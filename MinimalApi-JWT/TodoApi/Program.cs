using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TodoApi.Filters;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"];
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseSqlite(connectionString));

var securityScheme = new OpenApiSecurityScheme()
{
    Name = "Authorization",
    Type = SecuritySchemeType.ApiKey,
    Scheme = "Bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "JSON Web Token based security",
};

var securityReq = new OpenApiSecurityRequirement()
{
    {
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        },
        new string[] {}
    }
};

var contactInfo = new OpenApiContact()
{
    Name = "Mohamad Lawand",
    Email = "hello@mohamadlawand.com",
    Url = new Uri("https://mohamadlawand.com") 
};

var license = new OpenApiLicense()
{
    Name = "Free License",
};

var info = new OpenApiInfo()
{
    Version = "V1",
    Title = "Todo List Api with JWT Authentication",
    Description = "Todo List Api with JWT Authentication",
    Contact = contactInfo,
    License = license
};

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.SwaggerDoc("v1", info);
    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(securityReq);
});

builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer (options => {
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateAudience = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
        ValidateLifetime = false, // In any other application other then demo this needs to be true,
        ValidateIssuerSigningKey = true
    };
});

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// builder.Services.AddSingleton<ItemRepository>();
var app = builder.Build();

app.MapGet("/items", [Authorize] async (ApiDbContext db) =>
{
    return await db.Items.ToListAsync();
});

app.MapPost("/items", [Authorize] async (ApiDbContext db, Item item) =>
{
    if (await db.Items.FirstOrDefaultAsync(x => x.Id == item.Id) != null)
    {
        return Results.BadRequest();
    }

    db.Items.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/Items/{item.Id}", item);
}).AddFilter<ValidationFilter<Item>>();
//     .AddFilter((ctx, next) => async (context) =>
// {
//     return  CheckIncomingObj(context)  == false ? Results.BadRequest("Invalid parameters") : await next(context);
// });

// bool? CheckIncomingObj(RouteHandlerInvocationContext context) 
// {
//     if (context.Parameters[1] == null) 
//         return false;
//     
//     if (context.Parameters[1] is not Item param)
//         return false;
//
//     return IsValidItem(param);
// }
//
// bool IsValidItem(Item item)
// {
//     if (item.Title.Length > 1 && item.Id > 0)
//         return true;
//
//     return false;
// }

int? ParamCheck(RouteHandlerInvocationContext context) 
{

    if(context.Parameters.SingleOrDefault() != null)
    {
        int nb;
        var param =  context.Parameters.Single();
        var result = int.TryParse(param?.ToString(), out nb);
        return nb;
    }
    
    return null;
}

app.MapGet("/items/{id}", [Authorize] async (ApiDbContext db, int id) =>
{
    var item = await db.Items.FirstOrDefaultAsync(x => x.Id == id);

    return item == null ? Results.NotFound() : Results.Ok(item);
}).AddFilter((ctx, next) => async (context) =>
{
    return  ParamCheck(context)  != null ? Results.BadRequest("Invalid parameters") : await next(context);
});

app.MapPut("/items/{id}", [Authorize] async (ApiDbContext db, int id, Item item) =>
{
    var existItem = await db.Items.FirstOrDefaultAsync(x => x.Id == id);
    if(existItem == null)
    {
        return Results.BadRequest();
    }

    existItem.Title = item.Title;
    existItem.IsCompleted = item.IsCompleted;

    await db.SaveChangesAsync();
    return Results.Ok(item);
});

app.MapDelete("/items/{id}", [Authorize] async (ApiDbContext db, int id) => 
{
    var existItem = await db.Items.FirstOrDefaultAsync(x => x.Id == id);
    if(existItem == null)
    {
        return Results.BadRequest();
    }

    db.Items.Remove(existItem);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapPost("/accounts/login", [AllowAnonymous] (UserDto user) => {
    if(user.username == "admin@mohamadlawand.com" && user.password == "Password123")
    {
        var secureKey = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]);

        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        var securityKey = new SymmetricSecurityKey(secureKey);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512);

        var jwtTokenHandler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new [] {
                new Claim("Id", "1"),
                new Claim(JwtRegisteredClaimNames.Sub, user.username),
                new Claim(JwtRegisteredClaimNames.Email, user.username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            Expires = DateTime.Now.AddMinutes(5),
            Audience = audience,
            Issuer = issuer,
            SigningCredentials = credentials
        };

        var token = jwtTokenHandler.CreateToken(tokenDescriptor);
        var jwtToken = jwtTokenHandler.WriteToken(token);
        return Results.Ok(jwtToken);  
    }
    return Results.Unauthorized();
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello from Minimal API");
app.Run();

// record Item(int id, string title, bool IsCompleted);

record UserDto (string username, string password);

class Item
{
    public int Id { get; set; }
    public string Title { get; set; }
    public bool IsCompleted { get; set; }
}

// class ItemRepository
// {
//     private Dictionary<int, Item> items = new Dictionary<int, Item>();

//     public ItemRepository()
//     {
//         var item1 = new Item(1, "Go to the gym", false);
//         var item2 = new Item(2, "Drink Water", true);
//         var item3 = new Item(3, "Watch TV", false);

//         items.Add(item1.id, item1);
//         items.Add(item2.id, item2);
//         items.Add(item3.id, item3);
//     }

//     public IEnumerable<Item> GetAll() => items.Values;
//     public Item GetById(int id) {
//         if(items.ContainsKey(id))
//         {
//             return items[id];
//         }

//         return null;
//     }
//     public void Add(Item item) => items.Add(item.id, item);
//     public void Update(Item item) => items[item.id] = item;
//     public void Delete(int id) => items.Remove(id);
// }

class ApiDbContext : DbContext
{
    public DbSet<Item> Items { get; set; }

    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
    {
    }
}