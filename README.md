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

