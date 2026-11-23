using GymRatService.Common.DTOs.Export;
using GymRatService.Common.Enums;
using GymRatService.Common.Errors;
using GymRatService.Repository.Entities;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Services;
using NSubstitute;

namespace GymRatService.BLL.Tests.Services;

public class ExportServiceTests
{
    private readonly IUsersRepository _usersRepository = Substitute.For<IUsersRepository>();
    private readonly ISplitRepository _splitRepository = Substitute.For<ISplitRepository>();
    private readonly IExportRepository _exportRepository = Substitute.For<IExportRepository>();
    private readonly ExportService _service;

    public ExportServiceTests()
    {
        _service = new ExportService(_usersRepository, _splitRepository, _exportRepository);
    }

    [Theory]
    [InlineData("Bench", "Bench")]
    [InlineData("Bench, flat", "\"Bench, flat\"")]
    [InlineData("The \"big\" one", "\"The \"\"big\"\" one\"")]
    [InlineData("Line\r\nBreak", "\"Line\r\nBreak\"")]
    [InlineData("Line\nBreak", "\"Line\nBreak\"")]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("\tTab", "'\tTab")]
    [InlineData("\rReturn", "\"'\rReturn\"")]
    [InlineData(" =Space", " =Space")]
    [InlineData("=a,b", "\"'=a,b\"")]
    public async Task GetSetsCsvLinesAsync_EscapesTheExerciseColumn(string name, string expectedField)
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        _usersRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(new User { Id = userId });
        _exportRepository.StreamSetRows(userId).Returns(Rows(
            new ExportSetRowDto(sessionId, new DateTimeOffset(2026, 3, 1, 9, 30, 0, TimeSpan.FromHours(2)), "Push", name, 1, 8, 60.5m, 2, 62.5m, 8, ProgressionReason.AddRep)));

        // Act
        var lines = await ReadAsync(await _service.GetSetsCsvLinesAsync(userId, CancellationToken.None));

        // Assert
        Assert.Equal(2, lines.Count);
        Assert.Equal($"2026-03-01 07:30:00,Push,{expectedField},1,8,60.5,2,62.5,8,AddRep,{sessionId}", lines[1]);
    }

    [Fact]
    public async Task GetSetsCsvLinesAsync_LeavesOptionalColumnsEmpty_AndKeepsAMissingWorkoutNameEmpty()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        _usersRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(new User { Id = userId });
        _exportRepository.StreamSetRows(userId).Returns(Rows(
            new ExportSetRowDto(sessionId, new DateTimeOffset(2026, 3, 1, 9, 30, 0, TimeSpan.Zero), null, "Squat", 3, 5, 100m, null, null, null, null)));

        // Act
        var lines = await ReadAsync(await _service.GetSetsCsvLinesAsync(userId, CancellationToken.None));

        // Assert
        Assert.Equal($"2026-03-01 09:30:00,,Squat,3,5,100,,,,,{sessionId}", lines[1]);
    }

    [Fact]
    public async Task GetSetsCsvLinesAsync_ThrowsUserNotFound_BeforeReadingAnyRow()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        _usersRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessLogicException>(
            () => _service.GetSetsCsvLinesAsync(userId, CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.UserNotFound, exception.Code);
        _exportRepository.DidNotReceive().StreamSetRows(Arg.Any<Guid>());
    }

    private static async IAsyncEnumerable<ExportSetRowDto> Rows(params ExportSetRowDto[] rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }

        await Task.CompletedTask;
    }

    private static async Task<List<string>> ReadAsync(IAsyncEnumerable<string> lines)
    {
        var result = new List<string>();

        await foreach (var line in lines)
        {
            result.Add(line);
        }

        return result;
    }
}
