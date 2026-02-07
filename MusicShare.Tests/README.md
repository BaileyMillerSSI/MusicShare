# MusicShare.Tests

This project contains unit and integration tests for the MusicShare application.

## Testing Infrastructure

### Frameworks and Libraries

- **xUnit**: Test framework for writing and running tests
- **FluentAssertions**: Fluent assertion library for more readable test assertions
- **Moq**: Mocking library for isolating units under test
- **Aspire.Hosting.Testing**: Integration testing support for .NET Aspire applications
- **coverlet.collector**: Code coverage collection

### Running Tests

```bash
# Run all tests in the solution
dotnet test MusicShare.slnx

# Run only MusicShare.Tests
dotnet test MusicShare.Tests/MusicShare.Tests.csproj

# Run with code coverage
dotnet test MusicShare.Tests/MusicShare.Tests.csproj --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test MusicShare.Tests/MusicShare.Tests.csproj --filter "FullyQualifiedName~TestClassName"

# Run tests with detailed output
dotnet test MusicShare.Tests/MusicShare.Tests.csproj --logger "console;verbosity=detailed"
```

## Test Organization

```
MusicShare.Tests/
├── Infrastructure/           # Infrastructure and setup verification tests
├── Integration/             # Integration tests using Aspire
│   └── AspireIntegrationTestBase.cs  # Base class for Aspire integration tests
├── Unit/                    # Unit tests (to be added)
│   ├── Api/                 # API layer tests
│   │   ├── Commands/        # Command handler tests
│   │   ├── Queries/         # Query handler tests
│   │   └── Services/        # Service tests
│   ├── Worker/              # Worker layer tests
│   │   ├── Consumers/       # Message consumer tests
│   │   └── Sagas/           # Saga tests
│   ├── MusicAdapters/       # Music service adapter tests
│   └── Persistence/         # Repository tests
└── GlobalUsings.cs          # Global using directives
```

## Writing Tests

### Unit Tests

Unit tests should follow the Arrange-Act-Assert (AAA) pattern:

```csharp
public class MyServiceTests
{
    private readonly Mock<IDependency> _dependencyMock;
    private readonly MyService _sut; // System Under Test

    public MyServiceTests()
    {
        _dependencyMock = new Mock<IDependency>();
        _sut = new MyService(_dependencyMock.Object);
    }

    [Fact]
    public async Task MethodName_Scenario_ExpectedResult()
    {
        // Arrange
        var input = "test";
        _dependencyMock.Setup(x => x.DoSomething(input))
            .ReturnsAsync("result");

        // Act
        var result = await _sut.MethodName(input);

        // Assert
        result.Should().Be("result");
        _dependencyMock.Verify(x => x.DoSomething(input), Times.Once);
    }
}
```

### Integration Tests

Integration tests use the `AspireIntegrationTestBase` class to spin up the full application:

```csharp
public class ApiIntegrationTests : AspireIntegrationTestBase
{
    [Fact]
    public async Task Api_ShouldBeHealthy()
    {
        // Arrange
        var client = GetHttpClient("api");

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.Should().BeSuccessful();
    }
}
```

## Testing Best Practices

1. **Test Naming**: Use descriptive names following the pattern `MethodName_Scenario_ExpectedResult`
2. **One Assertion Focus**: Each test should verify one specific behavior
3. **Isolation**: Unit tests should not depend on external services (use mocks)
4. **Fast Execution**: Unit tests should run quickly; reserve slower integration tests for critical paths
5. **Readable Assertions**: Use FluentAssertions for clear, readable test assertions
6. **Arrange-Act-Assert**: Follow the AAA pattern for test structure
7. **Mock Verification**: Verify important interactions with mocked dependencies

## Future Enhancements

- Add more comprehensive unit test coverage for all layers
- Add end-to-end integration tests using Aspire
- Configure test parallelization settings
- Add mutation testing for test quality validation
- Set up continuous test reporting in CI/CD
