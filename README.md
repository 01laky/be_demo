# Be Demo API

ASP.NET Core WebAPI project with Identity framework.

## Features

- ASP.NET Core Identity for authentication and authorization
- Entity Framework Core with PostgreSQL
- Swagger/OpenAPI documentation
- RESTful API endpoints for registration, login, and logout

## Technologies

- .NET 10.0
- ASP.NET Core WebAPI
- Entity Framework Core 10.0
- ASP.NET Core Identity
- PostgreSQL

## Running

### Running in Docker container (recommended for development)

The easiest way to run using Docker:

```bash
./start-dev.sh
```

Or manually:

```bash
docker-compose -f docker-compose.dev.yml up --build
```

Application will be available at: `http://localhost:8000`
Swagger UI: `http://localhost:8000/swagger`

**Useful commands:**
- View logs: `docker-compose -f docker-compose.dev.yml logs -f`
- Stop: `docker-compose -f docker-compose.dev.yml down`
- Restart: `docker-compose -f docker-compose.dev.yml restart`

### Local run (without Docker)

1. Ensure PostgreSQL database is running (see `db_demo` folder)
2. Create database using migrations:
   ```bash
   cd BeDemo.Api
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
2. Run the application:
   ```bash
   dotnet run --launch-profile http
   ```
3. Open Swagger UI at: `http://localhost:8000/swagger`

## API Endpoints

- `POST /api/auth/register` - Register a new user
- `POST /api/auth/login` - Login
- `POST /api/auth/logout` - Logout
