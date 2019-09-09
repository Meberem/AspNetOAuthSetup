AspNetOAuthSetup
================

My exploration of how to setup an API that is protected using an OAuth service and how to setup that service

```
mkdir PROJECT-NAME
cd PROJECT-NAME
dotnet new sln
```

Create the OAuth service
------------------------
Some dotnet setup, this can be done through Visual Studio or via the command line, but here we create a project and add a few packages that we will be using. You can use 
```
mkdir src/AuthService
cd src/AuthService
dotnet new webapp
dotnet sln ..\..\PROJECT-NAME.sln add .\AuthService.csproj
dotnet add package IdentityServer4.AspNetIdentity -v 2.5.3
dotnet add package IdentityServer4.EntityFramework -v 2.5.3
dotnet add package Microsoft.EntityFrameworkCore.SqlServer -v 2.2.6
```

I've opened the project in Visual Studio and started working in there. I've added a user secret for the connection string (right click the project, manage user secrets) and set it up like so:

```
{
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\mssqllocaldb;Database=AuthService;Trusted_Connection=True;MultipleActiveResultSets=true;"
  }
}
```

(localdb) is stripped down sql server just for development, you'll want to use something more substaintial for production. So the reason this value is a secret is so that we don't accidentally end up with an empty development database when we deploy to production, we *have* to specify a value for this otherwise our application will scream into our logs.

with that out of the way, we can start adding some code.

Inital Code
-----------

First we're going to create a folder called `Models` and in that an `AppUser` which extends `IdentityUser` and an `AuthDbContext` which extends `IdentityDbContext<AppUser>`, right now both are pretty bare.

The meat of this step is in the `Startup.cs`, first we take an additional `IHostingEnvironment environment` in our Startup constructor and save that for later. Then we're going to the `ConfigureServices` method. Just before the `services.AddMvc()` call, we're going to do our stuff:

```
// Get the connection string we created earlier
var connectionString = Configuration.GetConnectionString("Default");

// Add the AuthDbContext to the service provider and tell it to use SQL server
services.AddDbContext<AuthDbContext>(o => o.UseSqlServer(connectionString));

services
	.AddIdentity<AppUser, IdentityRole>() // Adds ASP MVC identity services
	.AddEntityFrameworkStores<AuthDbContext>() // We are using Entity Framework to store user identity
	.AddDefaultUI() // We're using a default implementation of ASP MVC identity
	.AddDefaultTokenProviders(); // We have no special requirments for one time token generation 

// We have to use this as Identity Server has some dbContexts that we want to use, but we 
// the developer want to record the database migrations needed to setup the system
var thisAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;
var builder = services.AddIdentityServer();
// this adds the config data from DB (clients e.g. 1st Party web client, 3rd Party 
// app, resources e.g. Offical Api, Beta Api)
builder.AddConfigurationStore(options =>
	{
		options.ConfigureDbContext = b =>
			b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(thisAssembly));
	})
	// this adds the operational data from DB (codes, tokens, consents)
	.AddOperationalStore(options =>
	{
		options.ConfigureDbContext = b =>
			b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(thisAssembly));

		// this enables automatic token cleanup. this is optional.
		options.EnableTokenCleanup = true;
	})
	// Plumb MVC identity into the Identity server Implementation
	.AddAspNetIdentity<AppUser>();

if (Environment.IsDevelopment())
{
	builder.AddDeveloperSigningCredential();
}
else
{
	throw new Exception("Need to configure Production signing credentials");
}
```

Finally in the `Configure` method we are going to add
```
app.UseAuthentication(); // Each request can be authenticated using ASP MVC Identity
app.UseIdentityServer(); // We want this application to be an OAuth server
```

Add Migrations
--------------
Using Entity Framework we are going to setup the database, and generate some migrations so that the database structure can be recreated reliably.
```
dotnet ef migrations add Initial -c ConfigurationDbContext -o Migrations\ConfigurationDb
dotnet ef database update -c ConfigurationDbContext
dotnet ef migrations add Initial -c PersistedGrantDbContext -o Migrations\PersistedGrantDb
dotnet ef database update -c PersistedGrantDbContext
dotnet ef migrations add Initial -c AuthDbContext -o Migrations\AuthDb
dotnet ef database update -c AuthDbContext
```

There is one pair of commands for each db context, each migration is called Inital because I'm not sure what else to call the first one, and I have specified where the migration files should live with the `-o` options. As there is more than one DbContext in this project, 1 created and 2 imported from nuget packages, we need to specify which one we are using with the `-c` option.

Seed Data
---------
I'm keeping everything relating to seeding this project I have placed under /Seed and could probably be moved to a seperate application at some point but this will do for now. There is also an extension method that can be called on an `IWebHost` which will let us use a completely ready application to seed our data. 

First in `Program.cs` we check to see if there is the string `seed` in the arguments list, if there is yay! but don't pass it on to the rest of the application, build the WebHost then if we need to seed we will. To use it I have added a launch configuration that is much like the default but adds the argument that we're looking for. I have also prevented the browser from launching (I find it is a pain) and changed the default hosting url to `http://localhost:7000` because most of the time we will be developing on an API which will sit at the default port 5000.

The rest of this change is essentailly ready configuration data and selectively apply it to the relevant `DBSet` in the `ConfigurationDbContext`. The config is worth talking over, it is added to the DI system with the following call: `services.Configure<SeedOptions>(Configuration.GetSection("Seed"));` in `Startup.cs`, and the relevant config is as follows:
```
"Seed": {
    "AdminUsers": [ // This part of the configuration lives in the secrets.json managed by the secret manager
	  {
        "Email": "admin@myservice.com",
        "Password": "$Password123"
      }
	],
    "ApiResources": [
      {
        "Name": "PrimaryApi",
        "DisplayName": "The primary api",
        "Scopes": [ 
          {
			// Which scopes this API "owns", you could have "AdminScope" and "RegularUserScope" and then only permit "AdminApp1" and "3rdPartyAdminApp" to request "AdminScope" then only have "PublicFirstPartyClient" have access to "RegularUserScope"
            "Name": "PrimaryApiScope"
          }
        ]
      }
    ],
    "Clients": [
      {
        "ClientId": "48fafcf5-8f7c-4853-aee6-709405d162e0", // This doesn't really matter as long as it is unique
        "ClientName": "Primary Api Swagger Page",
        "AllowedGrantTypes": [ "implicit" ], // We need the simplist login type (here is username/password, gimme token) for a swagger endpoing
        "RedirectUris": [
          "http://localhost:5000/swagger/oauth2-redirect.html", // Where to send the token
          "https://production.myapi.com/swagger/oauth2-redirect.html" // There can be as many as needed
        ],
        "AllowedScopes": [
          "PrimaryApiScope" // The Scope that we want to have access too, what will be in our JWT token
        ],
        "AllowAccessTokensViaBrowser": true,
        "RequireConsent": false  // This is a 1st party application (and we haven't setup the UI for allowing consent) so we don't need user consent.
      }
    ]
  }
```

Now when we run the app, the database should be updated, we should then be able to visit 
`http://localhost:7000/connect/authorize/callback?response_type=token&state&client_id=48fafcf5-8f7c-4853-aee6-709405d162e0&scope=PrimaryApiScope&redirect_uri=http%3A%2F%2Flocalhost%3A5000%2Fswagger%2Foauth2-redirect.html`

We will then be prompted to login, then we'll be redirected to something like

`http://localhost:5000/swagger/oauth2-redirect.html#access_token=eyJhbGciOiJSUzI1NiIsImtpZCI6ImQ1Njk5MDIzMjBlOTY2YWM2NzNlZTc4ZGY5MTVkYjE2IiwidHlwIjoiSldUIn0.eyJuYmYiOjE1NjgwNTc0NjIsImV4cCI6MTU2ODA2MTA2MiwiaXNzIjoiaHR0cHM6Ly9sb2NhbGhvc3Q6NzAwMSIsImF1ZCI6WyJodHRwczovL2xvY2FsaG9zdDo3MDAxL3Jlc291cmNlcyIsIlByaW1hcnlBcGkiXSwiY2xpZW50X2lkIjoiNDhmYWZjZjUtOGY3Yy00ODUzLWFlZTYtNzA5NDA1ZDE2MmUwIiwic3ViIjoiMTZlMWY1ODQtYmViNy00ZDlkLTg2YzAtYzE2MDNhNGZlOTQyIiwiYXV0aF90aW1lIjoxNTY4MDU2MjM1LCJpZHAiOiJsb2NhbCIsInNjb3BlIjpbIlByaW1hcnlBcGlTY29wZSJdLCJhbXIiOlsicHdkIl19.c3niOUTjK9Pq9Kghx-u1mwVzR55b-FFlzh-X5smaENWssbW-58oaJvLw4yEoRqQ6yCf6VFtVobOinVQMXnFc6HcD4mfeElR8cTr27vv3xUv2Aif2byaeVjHLDhAeSB1O_qyXIGI43QwtWCjITlqEygGOnBHJ3KI0egqd6u8gGA7hlRzIFXfB653rgBrmZeB7l1rglRsObFbxnjwf2R-lvBNJaE7KKclFM1bALcizn44Xlff69O_bsD4b_Zoi3BlV3XCR2u2xJM4lifDiD-maarFs8wgYH88agtKojnxG-Z5QCLUFKwarZbFokQQtktqGs5UgTcJ1GXTc2-3cr4FMLg&token_type=Bearer&expires_in=3600&scope=PrimaryApiScope`

We can now use this token to ensure our Resource is protected in the next step.
