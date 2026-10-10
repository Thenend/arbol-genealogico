-- Árbol Genealógico (web): tablas, permisos y funciones para Supabase.
-- Se ejecuta una vez, entero, en el «SQL Editor» del proyecto de Supabase. Se puede volver a ejecutar sin perder datos.
--
-- Cada árbol es una fila con el mismo JSON que usa la aplicación de escritorio. Quién puede verlo o editarlo lo dice la
-- tabla «miembros» (propietario, editor o lector). Para compartir, el propietario apunta un correo en «invitaciones»: cuando
-- esa persona entra en la web con ese correo (aunque se registre más tarde), pasa a ser miembro con el rol indicado.

create extension if not exists pgcrypto;

-- ---------- Tablas ----------

create table if not exists public.arboles (
    id              uuid primary key default gen_random_uuid(),
    nombre          text not null default '',
    datos           jsonb not null,
    version         integer not null default 1,
    propietario     uuid not null default auth.uid() references auth.users (id) on delete cascade,
    creado          timestamptz not null default now(),
    actualizado     timestamptz not null default now(),
    actualizado_por text
);

create table if not exists public.miembros (
    arbol_id  uuid not null references public.arboles (id) on delete cascade,
    usuario   uuid not null references auth.users (id) on delete cascade,
    email     text not null default '',
    rol       text not null check (rol in ('propietario', 'editor', 'lector')),
    desde     timestamptz not null default now(),
    primary key (arbol_id, usuario)
);

create table if not exists public.invitaciones (
    arbol_id  uuid not null references public.arboles (id) on delete cascade,
    email     text not null,
    rol       text not null check (rol in ('editor', 'lector')),
    creada    timestamptz not null default now(),
    primary key (arbol_id, email)
);

create index if not exists miembros_usuario on public.miembros (usuario);
create index if not exists invitaciones_email on public.invitaciones (email);

-- ---------- Funciones auxiliares ----------

-- Rol del usuario actual en un árbol (null si no es miembro). «security definer» para que las reglas de acceso de
-- «miembros» puedan usarla sin llamarse a sí mismas.
create or replace function public.rol_en(p_arbol uuid)
returns text language sql stable security definer set search_path = public as $$
    select rol from public.miembros where arbol_id = p_arbol and usuario = auth.uid()
$$;

create or replace function public.mi_email()
returns text language sql stable as $$
    select lower(coalesce(auth.jwt() ->> 'email', ''))
$$;

-- Al crear un árbol, quien lo crea pasa a ser su propietario.
create or replace function public.al_crear_arbol()
returns trigger language plpgsql security definer set search_path = public as $$
begin
    insert into public.miembros (arbol_id, usuario, email, rol)
    values (new.id, new.propietario, public.mi_email(), 'propietario')
    on conflict do nothing;
    return new;
end $$;

drop trigger if exists al_crear_arbol on public.arboles;
create trigger al_crear_arbol after insert on public.arboles
    for each row execute function public.al_crear_arbol();

-- ---------- Reglas de acceso (RLS) ----------

alter table public.arboles enable row level security;
alter table public.miembros enable row level security;
alter table public.invitaciones enable row level security;

drop policy if exists "ver arboles" on public.arboles;
-- (también su propietario directamente: al crearlo, la fila se devuelve antes de que el disparador lo haga miembro)
create policy "ver arboles" on public.arboles for select using (propietario = auth.uid() or public.rol_en(id) is not null);
drop policy if exists "crear arboles" on public.arboles;
create policy "crear arboles" on public.arboles for insert with check (propietario = auth.uid());
drop policy if exists "editar arboles" on public.arboles;
create policy "editar arboles" on public.arboles for update using (public.rol_en(id) in ('propietario', 'editor'));
drop policy if exists "borrar arboles" on public.arboles;
create policy "borrar arboles" on public.arboles for delete using (public.rol_en(id) = 'propietario');

drop policy if exists "ver miembros" on public.miembros;
create policy "ver miembros" on public.miembros for select using (public.rol_en(arbol_id) is not null);
drop policy if exists "cambiar miembros" on public.miembros;
create policy "cambiar miembros" on public.miembros for update
    using (public.rol_en(arbol_id) = 'propietario') with check (rol in ('editor', 'lector'));
-- El propietario quita a quien quiera (menos a sí mismo); cualquiera puede salir de un árbol que no es suyo.
drop policy if exists "quitar miembros" on public.miembros;
create policy "quitar miembros" on public.miembros for delete
    using (rol <> 'propietario' and (public.rol_en(arbol_id) = 'propietario' or usuario = auth.uid()));

drop policy if exists "ver invitaciones" on public.invitaciones;
create policy "ver invitaciones" on public.invitaciones for select
    using (public.rol_en(arbol_id) = 'propietario' or lower(email) = public.mi_email());
drop policy if exists "crear invitaciones" on public.invitaciones;
create policy "crear invitaciones" on public.invitaciones for insert with check (public.rol_en(arbol_id) = 'propietario');
drop policy if exists "cambiar invitaciones" on public.invitaciones;
create policy "cambiar invitaciones" on public.invitaciones for update using (public.rol_en(arbol_id) = 'propietario');
drop policy if exists "borrar invitaciones" on public.invitaciones;
create policy "borrar invitaciones" on public.invitaciones for delete using (public.rol_en(arbol_id) = 'propietario');

-- ---------- Funciones que usa la web ----------

-- Convierte en miembro al usuario actual de todos los árboles a los que se invitó a su correo.
create or replace function public.aceptar_invitaciones()
returns integer language plpgsql security definer set search_path = public as $$
declare n integer;
begin
    if auth.uid() is null then return 0; end if;
    insert into public.miembros (arbol_id, usuario, email, rol)
    select i.arbol_id, auth.uid(), public.mi_email(), i.rol
    from public.invitaciones i where lower(i.email) = public.mi_email()
    on conflict (arbol_id, usuario) do nothing;
    get diagnostics n = row_count;
    delete from public.invitaciones where lower(email) = public.mi_email();
    return n;
end $$;

-- Los árboles del usuario (suyos y compartidos con él), sin el JSON entero.
create or replace function public.mis_arboles()
returns table (id uuid, nombre text, rol text, personas integer, actualizado timestamptz, actualizado_por text, propietario_email text)
language sql stable security definer set search_path = public as $$
    select a.id, a.nombre, m.rol, coalesce(jsonb_array_length(a.datos -> 'personas'), 0), a.actualizado, a.actualizado_por,
           (select p.email from public.miembros p where p.arbol_id = a.id and p.rol = 'propietario' limit 1)
    from public.arboles a join public.miembros m on m.arbol_id = a.id and m.usuario = auth.uid()
    order by a.actualizado desc
$$;

-- Guarda un árbol solo si nadie lo ha guardado desde que se abrió (misma versión). Devuelve la versión nueva, o -1 si
-- otra persona guardó antes (entonces no cambia nada y la web avisa).
create or replace function public.guardar_arbol(p_id uuid, p_nombre text, p_datos jsonb, p_version integer)
returns integer language plpgsql security invoker set search_path = public as $$
declare v integer;
begin
    if coalesce(public.rol_en(p_id), '') not in ('propietario', 'editor') then
        raise exception 'Sin permiso para editar este árbol' using errcode = '42501';
    end if;
    update public.arboles
       set datos = p_datos, nombre = p_nombre, version = version + 1, actualizado = now(), actualizado_por = public.mi_email()
     where id = p_id and (p_version is null or version = p_version)
    returning version into v;
    return coalesce(v, -1);
end $$;

-- Da a los miembros de un árbol el mismo acceso a otro (el árbol propio de un familiar, recién creado desde este), para
-- que quien veía uno pueda seguir el enlace al otro. Solo si el usuario es propietario del destino y miembro del origen.
create or replace function public.copiar_miembros(p_origen uuid, p_destino uuid)
returns integer language plpgsql security definer set search_path = public as $$
declare n integer;
begin
    if coalesce(public.rol_en(p_destino), '') <> 'propietario' or public.rol_en(p_origen) is null then
        raise exception 'Sin permiso' using errcode = '42501';
    end if;
    insert into public.miembros (arbol_id, usuario, email, rol)
    select p_destino, m.usuario, m.email, case when m.rol = 'propietario' then 'editor' else m.rol end
    from public.miembros m where m.arbol_id = p_origen
    on conflict (arbol_id, usuario) do nothing;
    get diagnostics n = row_count;
    insert into public.invitaciones (arbol_id, email, rol)
    select p_destino, i.email, i.rol from public.invitaciones i where i.arbol_id = p_origen
    on conflict do nothing;
    return n;
end $$;

grant execute on function public.rol_en(uuid), public.mi_email(), public.aceptar_invitaciones(), public.mis_arboles(),
    public.guardar_arbol(uuid, text, jsonb, integer), public.copiar_miembros(uuid, uuid) to authenticated;
revoke execute on function public.rol_en(uuid), public.aceptar_invitaciones(), public.mis_arboles(),
    public.guardar_arbol(uuid, text, jsonb, integer), public.copiar_miembros(uuid, uuid) from anon;

-- ---------- Directorio: familia y amigos ----------
-- Todos los usuarios de la web se ven entre sí (nombre y correo) para poder compartir árboles con facilidad. De los
-- árboles, los demás solo ven el nombre y cuántas personas tiene, y pueden pedir acceso; el propietario puede ocultar
-- un árbol del directorio («visible»).

create table if not exists public.perfiles (
    usuario  uuid primary key references auth.users (id) on delete cascade,
    email    text not null default '',
    nombre   text not null default '',
    creado   timestamptz not null default now(),
    visto    timestamptz not null default now()
);

alter table public.arboles add column if not exists visible boolean not null default true;

create table if not exists public.solicitudes (
    arbol_id  uuid not null references public.arboles (id) on delete cascade,
    usuario   uuid not null references auth.users (id) on delete cascade,
    email     text not null default '',
    nombre    text not null default '',
    mensaje   text not null default '',
    creada    timestamptz not null default now(),
    primary key (arbol_id, usuario)
);

-- los usuarios que ya existían
insert into public.perfiles (usuario, email)
select id, lower(coalesce(email, '')) from auth.users
on conflict (usuario) do nothing;

-- y los que se registren a partir de ahora
create or replace function public.al_crear_usuario()
returns trigger language plpgsql security definer set search_path = public as $$
begin
    insert into public.perfiles (usuario, email) values (new.id, lower(coalesce(new.email, '')))
    on conflict (usuario) do nothing;
    return new;
end $$;

drop trigger if exists al_crear_usuario on auth.users;
create trigger al_crear_usuario after insert on auth.users
    for each row execute function public.al_crear_usuario();

alter table public.perfiles enable row level security;
alter table public.solicitudes enable row level security;

drop policy if exists "ver perfiles" on public.perfiles;
create policy "ver perfiles" on public.perfiles for select to authenticated using (true);
drop policy if exists "cambiar mi perfil" on public.perfiles;
create policy "cambiar mi perfil" on public.perfiles for update to authenticated
    using (usuario = auth.uid()) with check (usuario = auth.uid());

drop policy if exists "ver solicitudes" on public.solicitudes;
create policy "ver solicitudes" on public.solicitudes for select
    using (usuario = auth.uid() or public.rol_en(arbol_id) = 'propietario');
drop policy if exists "borrar solicitudes" on public.solicitudes;
create policy "borrar solicitudes" on public.solicitudes for delete
    using (usuario = auth.uid() or public.rol_en(arbol_id) = 'propietario');

-- Al entrar: crea o pone al día el perfil del usuario (y, si se da, su nombre).
create or replace function public.entrar_perfil(p_nombre text default null)
returns void language plpgsql security definer set search_path = public as $$
begin
    if auth.uid() is null then return; end if;
    insert into public.perfiles (usuario, email, nombre)
    values (auth.uid(), public.mi_email(), coalesce(trim(p_nombre), ''))
    on conflict (usuario) do update
        set email = excluded.email, visto = now(),
            nombre = case when p_nombre is null then public.perfiles.nombre else trim(p_nombre) end;
end $$;

-- Todos los usuarios, con sus árboles visibles (o a los que ya se tiene acceso), el rol que tengo en cada uno y si ya
-- he pedido acceso.
create or replace function public.directorio()
returns table (usuario uuid, email text, nombre text, visto timestamptz, arboles jsonb)
language sql stable security definer set search_path = public as $$
    select p.usuario, p.email, p.nombre, p.visto,
           coalesce((
               select jsonb_agg(jsonb_build_object(
                          'id', a.id, 'nombre', a.nombre,
                          'personas', coalesce(jsonb_array_length(a.datos -> 'personas'), 0),
                          'rol', (select m.rol from public.miembros m where m.arbol_id = a.id and m.usuario = auth.uid()),
                          'solicitado', exists (select 1 from public.solicitudes s where s.arbol_id = a.id and s.usuario = auth.uid()))
                      order by a.nombre)
               from public.arboles a
               where a.propietario = p.usuario
                 and (a.visible or exists (select 1 from public.miembros m where m.arbol_id = a.id and m.usuario = auth.uid()))
           ), '[]'::jsonb)
    from public.perfiles p
    where auth.uid() is not null
    order by (p.usuario = auth.uid()) desc, lower(coalesce(nullif(p.nombre, ''), p.email))
$$;

-- Pedir acceso a un árbol visible (o cambiar el mensaje de una petición ya hecha).
create or replace function public.pedir_acceso(p_arbol uuid, p_mensaje text default '')
returns void language plpgsql security definer set search_path = public as $$
begin
    if auth.uid() is null or not exists (select 1 from public.arboles where id = p_arbol and visible) then
        raise exception 'Ese árbol no existe o no se puede pedir' using errcode = '42501';
    end if;
    if public.rol_en(p_arbol) is not null then return; end if;
    insert into public.solicitudes (arbol_id, usuario, email, nombre, mensaje)
    select p_arbol, auth.uid(), public.mi_email(), coalesce((select nombre from public.perfiles where usuario = auth.uid()), ''), coalesce(p_mensaje, '')
    on conflict (arbol_id, usuario) do update set mensaje = excluded.mensaje, creada = now();
end $$;

-- Las peticiones de acceso a mis árboles.
create or replace function public.mis_solicitudes()
returns table (arbol_id uuid, arbol text, usuario uuid, email text, nombre text, mensaje text, creada timestamptz)
language sql stable security definer set search_path = public as $$
    select s.arbol_id, a.nombre, s.usuario, s.email, coalesce(nullif(p.nombre, ''), s.nombre), s.mensaje, s.creada
    from public.solicitudes s
    join public.arboles a on a.id = s.arbol_id
    left join public.perfiles p on p.usuario = s.usuario
    where public.rol_en(s.arbol_id) = 'propietario'
    order by s.creada
$$;

-- Dar acceso a un árbol mío a un usuario de la web (sin invitación: lo ve al momento). Si había pedido acceso, la
-- petición queda atendida. Si ya era miembro, solo cambia su rol.
create or replace function public.compartir_con(p_arbol uuid, p_usuario uuid, p_rol text)
returns void language plpgsql security definer set search_path = public as $$
begin
    if coalesce(public.rol_en(p_arbol), '') <> 'propietario' then
        raise exception 'Solo el propietario puede compartir este árbol' using errcode = '42501';
    end if;
    if p_rol not in ('editor', 'lector') then raise exception 'Rol no válido'; end if;
    if p_usuario = auth.uid() then return; end if;
    insert into public.miembros (arbol_id, usuario, email, rol)
    select p_arbol, p.usuario, p.email, p_rol from public.perfiles p where p.usuario = p_usuario
    on conflict (arbol_id, usuario) do update set rol = excluded.rol
        where public.miembros.rol <> 'propietario';
    delete from public.solicitudes where arbol_id = p_arbol and usuario = p_usuario;
    delete from public.invitaciones i using public.perfiles p
     where i.arbol_id = p_arbol and p.usuario = p_usuario and lower(i.email) = p.email;
end $$;

-- Mostrar u ocultar un árbol mío en el directorio.
create or replace function public.poner_visible(p_arbol uuid, p_visible boolean)
returns void language plpgsql security definer set search_path = public as $$
begin
    if coalesce(public.rol_en(p_arbol), '') <> 'propietario' then
        raise exception 'Solo el propietario puede cambiar esto' using errcode = '42501';
    end if;
    update public.arboles set visible = p_visible where id = p_arbol;
end $$;

grant select, update on public.perfiles to authenticated;
grant select, delete on public.solicitudes to authenticated;
grant execute on function public.entrar_perfil(text), public.directorio(), public.pedir_acceso(uuid, text),
    public.mis_solicitudes(), public.compartir_con(uuid, uuid, text), public.poner_visible(uuid, boolean) to authenticated;
revoke execute on function public.entrar_perfil(text), public.directorio(), public.pedir_acceso(uuid, text),
    public.mis_solicitudes(), public.compartir_con(uuid, uuid, text), public.poner_visible(uuid, boolean), public.al_crear_usuario() from anon, public;
grant execute on function public.entrar_perfil(text), public.directorio(), public.pedir_acceso(uuid, text),
    public.mis_solicitudes(), public.compartir_con(uuid, uuid, text), public.poner_visible(uuid, boolean) to authenticated;

-- ---------- Tiempo real ----------
-- Para que, si otro familiar guarda cambios, el árbol abierto se actualice solo.
do $$
begin
    if not exists (select 1 from pg_publication_tables where pubname = 'supabase_realtime' and schemaname = 'public' and tablename = 'arboles') then
        alter publication supabase_realtime add table public.arboles;
    end if;
exception when undefined_object then null;   -- sin la publicación de Supabase (p. ej. en otra base de datos): sin tiempo real
end $$;

-- Que la API de Supabase vea ya las tablas y funciones nuevas.
notify pgrst, 'reload schema';
