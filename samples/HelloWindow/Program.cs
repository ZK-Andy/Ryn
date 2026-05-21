using Ryn.Core;

var app = RynApplication.CreateBuilder(new RynOptions
{
    Title = "Hello Ryn!",
    Width = 1024,
    Height = 768,
    Url = new Uri("https://example.com"),
    DevTools = true,
}).Build();

await app.RunAsync();
