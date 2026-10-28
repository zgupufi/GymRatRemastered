# Backend

## Architecture (N-layer: Controller → Service → Repository)
```
src/
  GymRatService.API/          controllers, filters, extensions, DI setup
  GymRatService.Service/      services, validators, security, service interfaces
  GymRatService.Repository/   entities, EF Core DbContext, repositories and interfaces
  GymRatService.Common/       DTOs, error codes, settings, shared enums/constants
tests/
  GymRatService.BLL.Tests/
  GymRatService.IntegrationTests/
```

Dependency rule (do not violate):
- Direction is `API → Service → Repository`; every layer may reference `Common`. Never the reverse.
- Controllers map HTTP request → service call → HTTP response. No business logic, no direct DB access.
- Services hold business logic. They depend on repository interfaces, never on `DbContext`.
- Repositories do data access only (EF Core queries), no business rules.
- Entities live in `Repository`. The API exposes DTOs only, never entities.

## Controllers
- `[Authorize]` goes right after `[ApiController]`. Anonymous endpoints opt out with `[AllowAnonymous]`.
- No `[ProducesResponseType]` attributes.
- Routes use plain `{id}` with no route constraint.
- Name actions after what they return (`GetUserProfileAsync`, not `GetMeAsync`).
- Store the service result in a `response` variable and return `Ok(response)`; never nest the service call inside `Ok(...)`.
- Status codes:
  - GET returns `Ok(response)`.
  - Update, delete and other posts return `NoContent()`.
  - A POST that creates a resource the client needs to reference returns 201 Created with the new id; otherwise a plain `Created()` with no body.
- Get the current user's id with `User.GetUserId()` (`API/Extensions/ClaimsPrincipalExtensions`).

## Coding conventions
- Async methods end in `Async` and take a `CancellationToken`, passed all the way down to EF Core.
- Nullable reference types stay enabled; avoid the `!` null-forgiving operator.
- One class per file; folder structure matches namespace.
- New endpoints get request/response DTOs unless they are not needed. Ask if unsure.
- Entities have a blank line after each property.
- Ids are GUID v7 and the database generates them: Postgres default `uuid_generate_v7()`, EF config `ValueGeneratedOnAdd().HasDefaultValueSql("uuid_generate_v7()")`, no initializer on the entity. The SQLite test database registers the same function.

## Errors, validation and messages
- Business errors throw `ServiceException(code, message)`. `ServiceExceptionFilter` turns it into a 400 with `{ code, message }`.
- Error codes are `Area.Name` constants in `Common/Errors/ErrorCodes.cs` (e.g. `Auth.InvalidCredentials`, `Users.NotFound`), so an error can be traced to where it was thrown.
- Request input validation uses FluentValidation validators in `Service/Validators`, with `WithErrorCode` per rule and `ValidateOrThrowAsync`. No hand-written checks in services.
- Every user-facing message comes from resx files localized in English and Romanian (`X.resx` + `X.ro.resx`), including validator messages and exceptions thrown in the API layer.

## Security
- Passwords are hashed with salt and pepper using .NET's own crypto (PBKDF2), no third-party hashing or auth libraries.
- Secrets stay out of `appsettings*.json`. Locally use user secrets on the API project:
  - `dotnet user-secrets set "JwtSettings:Key" "<key>"`
  - `dotnet user-secrets set "PasswordSettings:Pepper" "<pepper>"`
  - Docker reads them from `infra/.env` instead.

## Commands (run from `/backend`)
- Build: `dotnet build`
- Unit and integration tests: `dotnet test`

## Testing
- Service logic: xUnit unit tests, mocking repository interfaces.
- Filters, validators and pagination: xUnit unit tests.
- Endpoint integration tests use Arrange-Act-Assert. Successful calls assert the returned data, not just the status code. Failing calls assert the expected error code.
