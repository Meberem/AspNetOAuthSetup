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

