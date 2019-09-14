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

Protecting the Api
------------------
So first of all I'm going to make some config changes, https will be used by default which means the port numbers change from 5000 -> 5001 and 7000 -> 7001. I updated the seed config data in the AuthService appsettings.json but you can just update the dbo.ClinetRedirectUris table.

With that out of the way onto the code.

I created a new Asp MVC project and added it to the solution, it is fairly bare with a single `Controllers/ValuesController.cs`, `Program.cs`, `Startup.cs` and `appsettings.json`. We don't need to touch Program.cs and only one change is needed to Values controller:
```
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
 	[Authorize]
	[Route("api/[controller]")]
	[ApiController]
	public class ValuesController : ControllerBase
	{
...
```
The Authorize attribute means all request going to this controller must from a User, what a User is is down to us, we're going to define that in just a moment, we need some config values first:
```
"Authentication": {
    "Authority": "https://localhost:7001",
    "ClientId": "48fafcf5-8f7c-4853-aee6-709405d162e0",
    "Audience": "PrimaryApi",
	"Scope": "PrimaryApiScope"
}
```
Adding this section to `appsettings.json` keeps our code clean of magic variables.

- `Authority` is an OpenIdConnect term for who we, the application, trust. How the application does that is by speaking to its OpenId Configuration, we can see what this looks like by going to `https://localhost:7001/.well-known/openid-configuration` when the AuthService is running, ASP MVC can use this "introspection endpoint" to do a lot of the heavy lifting for us. 
- `ClientId` is our client, as the API isn't going to be involved in the Authentication dance we need to tell the AuthService who we are. Using this Id the AuthService will validate that the request the User will make to login will be one we would approve of, it does this by looking at the `redirect_uri` and it must be one defined in our Client definition (currently in the seed data).
- `Audience` is something we are going to use to verify that the token recieved from the AuthService is "correct" as in the token has been made for "us" because we are the Resource being protected. In this case the token must have been made to access the PrimaryApi.
- `Scope` in a very similar manner to `Audience` we need the token to contain this scope value or we will reject it as invalid. Scope can be shared between multiple audiences, think of "SeeAccountProfile" scope.

Now its going to get complicated... Onto `Startup.cs`! First in `ConfigureServices`:
```
public void ConfigureServices(IServiceCollection services)
{
	// I have added this to help me debug things, it is not necessary and should not be enabled for production
	IdentityModelEventSource.ShowPII = true;
	services
		// We want to Authenticate that requests have come from someone we trust
		.AddAuthentication(options =>
		{
			// We are going to do this by looking for asking the middleware with the name: JwtBearerDefaults.AuthenticationScheme (Bearer) to look into this for us
			options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
			// We are also going to ask the middleware to attempt to authenticate requests whenever it can
			options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
		})
		// This is the middleware in question, if we gave this a name (first param) then we would have to update the DefaultAuthenticateScheme and DefaultChallengeScheme to have the same value.
		.AddJwtBearer(options =>
		{
			// We expect the token to be for the correct Resource - us!
			options.Audience = Configuration["Authentication:Audience"];
			// This is who we trust to provide this info, as described above we will validate the token using the information from the "introspection endpoint". This will also be used to 
			options.Authority = Configuration["Authentication:Authority"];
		});

	services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
}
```

now in `Configure` we need to add a single line:
```
// We want all requests from this point onwards to be protected by the authentication middleware if necessary
app.UseAuthentication();
app.UseHttpsRedirection();
app.UseMvc();
```

To try and make it a bit easier I'm going to install `NSwag.AspNetCore` currently at 13.0.6 so we can see a friendly page where we can see Auth working/not working. In `ConfigureServices` we can add:

```
// This is a "standard" (one of many) on how to document APIs, it pulls information from our Controllers to generate this
services.AddOpenApiDocument(options =>
{
	// Our API has protection (I'm calling it the same thing as our AuthenicationScheme for consistency)
	options.AddSecurity(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
	{
		// We are the unified modern OAuth2 implementation provided by IdentityServer4
		Type = OpenApiSecuritySchemeType.OAuth2,
		// Because the UI has an outstanding issue we need to tell it to use a different token name for reasons...
		ExtensionData = new Dictionary<string, object>
		{
			["x-tokenName"] = "id_token"
		},
		Description = "Authenticate using our Auth Service",
		// Sadly the UI can't use the introspection endpoint so we have to set this ourself
		AuthorizationUrl = $"{Configuration["Authentication:Authority"]}/connect/authorize",
		// We need to say exactly which scopes we want the user to access, glad it is in a Configuration value now!
		Scopes = new Dictionary<string, string>
		{
			[$"{Configuration["Authentication:Scope"]}"] = "The Scope we need the token to have"
		}
	});

	// Now we hook anything with an [Authorize] attribute into the definition we created above. NSwag does this like so:
	options.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor(JwtBearerDefaults.AuthenticationScheme));
});
```

Now to `Configure` somewhere before `UseMvc()` we need to add
```
app
	// we are adding the generated OpenApi document at the URI /swagger/v1/swagger.json
	.UseOpenApi() 
	// add /swagger (remember from the seed data?! where the friendly UI is added 
	.UseSwaggerUi3(settings =>
	{
		// We do however need to tell it that we know some details to help the user login
		settings.OAuth2Client = new OAuth2ClientSettings
		{
			ClientId = Configuration["Authentication:ClientId"],
		};
	});
```

Phew! Now we can set our two projects to startup up at once, right click the solution file > "Set Starup Projects", then choose "Multiple startup projects, then for each project dropdown choose "Start". Oh I also need to update the `launchSettings.json` for the new API before we run
```
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "profiles": {
    "Api": {
      "commandName": "Project",
      "launchBrowser": false,
      "launchUrl": "api/values",
      "applicationUrl": "https://localhost:5001;http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

Now we should be able to click "Start" or F5 to begin. Now we should be able to visit:
https://localhost:5001/swagger 
Now if you expand the "Values" pane, you should see an unlocked padlock on the 5 operations available to you. Expand any one of those, click "Try it out", then click "Execute", you should see something like
```
401 Undocumented
Error: Unauthorized
Response headers

content-length: 0 
date: Sat, 14 Sep 2019 10:13:27 GMT 
server: Kestrel  www-authenticate: Bearer 
```

Now click on Authorize, check the scope you want to access (you do want to access it), then click authorize. A new tab will open, you may be asked to login if you weren't already, but if you already are logged in or after you login/create an account you will jump back to the page with an action to "Logout". We can just dismiss this prompt and click "Execute" again, if all is well then we should get back
```
200	
Response body

[
  "value1",
  "value2"
]

Response headers

 content-type: application/json; charset=utf-8 
 date: Sat, 14 Sep 2019 10:16:13 GMT
 server: Kestrel  transfer-encoding: chunked 
```