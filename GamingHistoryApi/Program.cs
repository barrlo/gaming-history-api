var builder = WebApplication.CreateBuilder(args);

// builder.Services.AddCors(options =>
// {
//     options.AddDefaultPolicy(policy =>
//     {
//         policy.WithOrigins("https://gaming-history.barrlo.net"); // , "http://localhost:3000");
//     });
// });

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
app.UseSwagger();
app.UseSwaggerUI();

// var  localOrigins = "LocalOrigins";
// builder.Services.AddCors(options =>
// {
//     options.AddPolicy(localOrigins, policy =>
//     {
//         policy.WithOrigins("https://localhost:3000");
//     });
// });
// app.UseCors(localOrigins);
// }

// app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();