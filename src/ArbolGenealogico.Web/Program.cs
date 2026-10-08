using ArbolGenealogico.Web;
using ArbolGenealogico.Web.Servicios;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var config = new ConfigSupabase
{
    Url = builder.Configuration["Supabase:Url"] ?? "",
    Clave = builder.Configuration["Supabase:Clave"] ?? "",
};
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<Datos>();

await builder.Build().RunAsync();
