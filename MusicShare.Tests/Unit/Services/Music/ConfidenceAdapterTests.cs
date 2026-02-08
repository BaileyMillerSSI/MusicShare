using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;
using MusicShare.Services.Services;
using MusicShare.Services.Services.Music;
using Microsoft.Extensions.Logging;

namespace MusicShare.Tests.Unit.Services.Music;

public class ConfidenceAdapterTests
{
    #region Basic Filtering Tests

    [Fact]
    public async Task ItWillReturnHighestConfidenceMatch()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var lowMatch = new SongSearchResult(
            "https://spotify.com/track/low",
            new SongMetadata
            {
                Title = "Similar Song",
                Artists = ["Test Artist"],
                Album = "Test Album",
                DurationMs = 180000
            });

        var highMatch = new SongSearchResult(
            "https://spotify.com/track/high",
            new SongMetadata
            {
                Title = "Test Song",
                Artists = ["Test Artist"],
                Album = "Test Album",
                DurationMs = 180000
            });

        var mediumMatch = new SongSearchResult(
            "https://spotify.com/track/medium",
            new SongMetadata
            {
                Title = "Test Song",
                Artists = ["Different Artist"],
                Album = "Test Album",
                DurationMs = 180000
            });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(lowMatch, mediumMatch, highMatch));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        // Score them in different order to verify sorting
        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, lowMatch.FoundMetadata))
            .Returns(CreateScore(0.70));

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, highMatch.FoundMetadata))
            .Returns(CreateScore(0.95));

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, mediumMatch.FoundMetadata))
            .Returns(CreateScore(0.75));

        var sut = mock.Create<ConfidenceAdapter>();

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            results.Add(result);
        }

        results.Should().HaveCount(3);
        results[0].Url.Should().Be("https://spotify.com/track/high"); // Best match first
        results[1].Url.Should().Be("https://spotify.com/track/medium");
        results[2].Url.Should().Be("https://spotify.com/track/low");
    }

    [Fact]
    public async Task ItWillReturnNullIfAllBelowThreshold()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var lowMatch = new SongSearchResult(
            "https://spotify.com/track/1",
            new SongMetadata
            {
                Title = "Different",
                Artists = ["Different"],
                Album = "Different",
                DurationMs = 200000
            });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(lowMatch));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, lowMatch.FoundMetadata))
            .Returns(CreateScore(0.50)); // Below 0.65 threshold

        var sut = mock.Create<ConfidenceAdapter>();

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            results.Add(result);
        }

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ItWillReturnFirstResultIfAboveThreshold()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var goodMatch = new SongSearchResult(
            "https://spotify.com/track/1",
            new SongMetadata
            {
                Title = "Test Song",
                Artists = ["Test Artist"],
                Album = "Test Album",
                DurationMs = 180000
            });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(goodMatch));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, goodMatch.FoundMetadata))
            .Returns(CreateScore(0.85));

        var sut = mock.Create<ConfidenceAdapter>();

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            results.Add(result);
        }

        results.Should().ContainSingle();
        results[0].Url.Should().Be("https://spotify.com/track/1");
    }

    [Fact]
    public async Task ItWillReturnNullForEmptyResults()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults()); // Empty results

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        var sut = mock.Create<ConfidenceAdapter>();

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            results.Add(result);
        }

        results.Should().BeEmpty();
    }

    #endregion

    #region Confidence Calculation Tests

    [Fact]
    public async Task ItWillCallConfidenceScoreServiceForEachResult()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var result1 = new SongSearchResult(
            "https://spotify.com/track/1",
            new SongMetadata { Title = "Song 1", Artists = ["Artist 1"], Album = "Album 1", DurationMs = 180000 });

        var result2 = new SongSearchResult(
            "https://spotify.com/track/2",
            new SongMetadata { Title = "Song 2", Artists = ["Artist 2"], Album = "Album 2", DurationMs = 180000 });

        var result3 = new SongSearchResult(
            "https://spotify.com/track/3",
            new SongMetadata { Title = "Song 3", Artists = ["Artist 3"], Album = "Album 3", DurationMs = 180000 });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(result1, result2, result3));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        var scoringService = mock.Mock<IConfidenceScoreService>();
        scoringService
            .Setup(x => x.CalculateScore(It.IsAny<SongMetadata>(), It.IsAny<SongMetadata>()))
            .Returns(CreateScore(0.85));

        var sut = mock.Create<ConfidenceAdapter>();

        await foreach (var _ in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            // Consume results
        }

        scoringService.Verify(x => x.CalculateScore(sourceMetadata, result1.FoundMetadata), Times.Once);
        scoringService.Verify(x => x.CalculateScore(sourceMetadata, result2.FoundMetadata), Times.Once);
        scoringService.Verify(x => x.CalculateScore(sourceMetadata, result3.FoundMetadata), Times.Once);
    }

    [Fact]
    public async Task ItWillScoreEachCandidateBeforeFiltering()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var highScore = new SongSearchResult(
            "https://spotify.com/track/high",
            new SongMetadata { Title = "High", Artists = ["Artist"], Album = "Album", DurationMs = 180000 });

        var lowScore = new SongSearchResult(
            "https://spotify.com/track/low",
            new SongMetadata { Title = "Low", Artists = ["Artist"], Album = "Album", DurationMs = 180000 });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(lowScore, highScore));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        var scoringService = mock.Mock<IConfidenceScoreService>();
        scoringService
            .Setup(x => x.CalculateScore(sourceMetadata, lowScore.FoundMetadata))
            .Returns(CreateScore(0.40)); // Below threshold

        scoringService
            .Setup(x => x.CalculateScore(sourceMetadata, highScore.FoundMetadata))
            .Returns(CreateScore(0.90)); // Above threshold

        var sut = mock.Create<ConfidenceAdapter>();

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            results.Add(result);
        }

        // Both should be scored even though one is filtered out
        scoringService.Verify(x => x.CalculateScore(sourceMetadata, lowScore.FoundMetadata), Times.Once);
        scoringService.Verify(x => x.CalculateScore(sourceMetadata, highScore.FoundMetadata), Times.Once);

        // Only high score should be returned
        results.Should().ContainSingle();
        results[0].Url.Should().Be("https://spotify.com/track/high");
    }

    [Fact]
    public async Task ItWillSortResultsByConfidenceDescending()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var result1 = new SongSearchResult("https://spotify.com/track/1",
            new SongMetadata { Title = "1", Artists = ["A"], Album = "A", DurationMs = 180000 });
        var result2 = new SongSearchResult("https://spotify.com/track/2",
            new SongMetadata { Title = "2", Artists = ["A"], Album = "A", DurationMs = 180000 });
        var result3 = new SongSearchResult("https://spotify.com/track/3",
            new SongMetadata { Title = "3", Artists = ["A"], Album = "A", DurationMs = 180000 });

        // Return in arbitrary order
        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(result2, result1, result3));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        // Assign scores in different order
        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, result1.FoundMetadata))
            .Returns(CreateScore(0.95)); // Highest

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, result2.FoundMetadata))
            .Returns(CreateScore(0.70)); // Lowest

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, result3.FoundMetadata))
            .Returns(CreateScore(0.85)); // Middle

        var sut = mock.Create<ConfidenceAdapter>();

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            results.Add(result);
        }

        results.Should().HaveCount(3);
        results[0].Url.Should().Be("https://spotify.com/track/1"); // 0.95
        results[1].Url.Should().Be("https://spotify.com/track/3"); // 0.85
        results[2].Url.Should().Be("https://spotify.com/track/2"); // 0.70
    }

    #endregion

    #region Threshold Testing

    [Fact]
    public async Task ItWillFilterOutLowConfidenceResults()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var high = new SongSearchResult("https://spotify.com/track/high",
            new SongMetadata { Title = "H", Artists = ["A"], Album = "A", DurationMs = 180000 });
        var medium = new SongSearchResult("https://spotify.com/track/medium",
            new SongMetadata { Title = "M", Artists = ["A"], Album = "A", DurationMs = 180000 });
        var low = new SongSearchResult("https://spotify.com/track/low",
            new SongMetadata { Title = "L", Artists = ["A"], Album = "A", DurationMs = 180000 });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(high, medium, low));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, high.FoundMetadata))
            .Returns(CreateScore(0.90));

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, medium.FoundMetadata))
            .Returns(CreateScore(0.70));

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, low.FoundMetadata))
            .Returns(CreateScore(0.45)); // Below 0.65 threshold

        var sut = mock.Create<ConfidenceAdapter>();

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            results.Add(result);
        }

        results.Should().HaveCount(2);
        results.Should().Contain(r => r.Url == "https://spotify.com/track/high");
        results.Should().Contain(r => r.Url == "https://spotify.com/track/medium");
        results.Should().NotContain(r => r.Url == "https://spotify.com/track/low");
    }

    [Fact]
    public async Task ItWillUseDefaultThreshold0_65()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var exactThreshold = new SongSearchResult("https://spotify.com/track/exact",
            new SongMetadata { Title = "T", Artists = ["A"], Album = "A", DurationMs = 180000 });
        var justBelow = new SongSearchResult("https://spotify.com/track/below",
            new SongMetadata { Title = "T", Artists = ["A"], Album = "A", DurationMs = 180000 });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(exactThreshold, justBelow));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, exactThreshold.FoundMetadata))
            .Returns(CreateScore(0.65)); // Exactly at threshold

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, justBelow.FoundMetadata))
            .Returns(CreateScore(0.649)); // Just below

        var sut = mock.Create<ConfidenceAdapter>();

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            results.Add(result);
        }

        results.Should().ContainSingle();
        results[0].Url.Should().Be("https://spotify.com/track/exact");
    }

    [Fact]
    public async Task ItWillRespectCustomThreshold()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var mediumScore = new SongSearchResult("https://spotify.com/track/medium",
            new SongMetadata { Title = "T", Artists = ["A"], Album = "A", DurationMs = 180000 });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(mediumScore));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, mediumScore.FoundMetadata))
            .Returns(CreateScore(0.70)); // Above default 0.65, below custom 0.80

        // Use custom threshold of 0.80
        var innerAdapter = mock.Mock<IMusicServiceAdapter>().Object;
        var confidenceService = mock.Mock<IConfidenceScoreService>().Object;
        var logger = mock.Mock<ILogger<ConfidenceAdapter>>().Object;
        var sut = new ConfidenceAdapter(innerAdapter, confidenceService, logger, confidenceThreshold: 0.80);

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            results.Add(result);
        }

        results.Should().BeEmpty(); // 0.70 is below 0.80 threshold
    }

    [Fact]
    public async Task ItWillReturnNullIfBestScoreJustBelowThreshold()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var justBelow = new SongSearchResult("https://spotify.com/track/1",
            new SongMetadata { Title = "T", Artists = ["A"], Album = "A", DurationMs = 180000 });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(justBelow));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, justBelow.FoundMetadata))
            .Returns(CreateScore(0.64)); // Just below 0.65

        var sut = mock.Create<ConfidenceAdapter>();

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            results.Add(result);
        }

        results.Should().BeEmpty();
    }

    #endregion

    #region Delegation Tests

    [Fact]
    public async Task ItWillDelegateToInnerAdapterFindSongs()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var innerAdapter = mock.Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults());

        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        var sut = mock.Create<ConfidenceAdapter>();

        await foreach (var _ in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            // Consume
        }

        innerAdapter.Verify(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ItWillDelegateDependentMethodsToInnerAdapter()
    {
        using var mock = AutoMock.GetLoose();

        var innerAdapter = mock.Mock<IMusicServiceAdapter>();
        innerAdapter.Setup(x => x.ServiceType).Returns(ServiceType.AppleMusic);
        innerAdapter.Setup(x => x.NormalizeUrl("test-url")).Returns("normalized-url");
        innerAdapter.Setup(x => x.ExtractSongId("test-url")).Returns("song-123");

        var sut = mock.Create<ConfidenceAdapter>();

        sut.ServiceType.Should().Be(ServiceType.AppleMusic);
        sut.NormalizeUrl("test-url").Should().Be("normalized-url");
        sut.ExtractSongId("test-url").Should().Be("song-123");

        innerAdapter.Verify(x => x.ServiceType, Times.AtLeastOnce);
        innerAdapter.Verify(x => x.NormalizeUrl("test-url"), Times.Once);
        innerAdapter.Verify(x => x.ExtractSongId("test-url"), Times.Once);
    }

    [Fact]
    public async Task ItWillPropagateInnerAdapterExceptions()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(ThrowAsync());

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        var sut = mock.Create<ConfidenceAdapter>();

        var act = async () =>
        {
            await foreach (var _ in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
            {
                // Should throw before yielding
            }
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Inner adapter failed");
    }

    [Fact]
    public async Task ItWillDelegateResolveMetadataAsync()
    {
        using var mock = AutoMock.GetLoose();

        var expectedMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var innerAdapter = mock.Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ResolveMetadataAsync("test-url", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetadata);

        var sut = mock.Create<ConfidenceAdapter>();

        var result = await sut.ResolveMetadataAsync("test-url", CancellationToken.None);

        result.Should().Be(expectedMetadata);
        innerAdapter.Verify(x => x.ResolveMetadataAsync("test-url", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task ItWillLogDebugForEachCandidate()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var result1 = new SongSearchResult("https://spotify.com/track/1",
            new SongMetadata { Title = "1", Artists = ["A"], Album = "A", DurationMs = 180000 });
        var result2 = new SongSearchResult("https://spotify.com/track/2",
            new SongMetadata { Title = "2", Artists = ["A"], Album = "A", DurationMs = 180000 });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(result1, result2));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(It.IsAny<SongMetadata>(), It.IsAny<SongMetadata>()))
            .Returns(CreateScore(0.85));

        var logger = mock.Mock<ILogger<ConfidenceAdapter>>();
        var sut = mock.Create<ConfidenceAdapter>();

        await foreach (var _ in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            // Consume
        }

        // Verify debug logs were called (at least once per candidate)
        logger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Evaluated candidate")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task ItWillLogWarningWhenAllFilteredOut()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var lowScore = new SongSearchResult("https://spotify.com/track/1",
            new SongMetadata { Title = "L", Artists = ["A"], Album = "A", DurationMs = 180000 });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(lowScore));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, lowScore.FoundMetadata))
            .Returns(CreateScore(0.40)); // Below threshold

        var logger = mock.Mock<ILogger<ConfidenceAdapter>>();
        var sut = mock.Create<ConfidenceAdapter>();

        await foreach (var _ in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            // Should yield nothing
        }

        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("filtered out")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillLogInformationOnSuccess()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var goodMatch = new SongSearchResult("https://spotify.com/track/1",
            new SongMetadata { Title = "T", Artists = ["A"], Album = "A", DurationMs = 180000 });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(goodMatch));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, goodMatch.FoundMetadata))
            .Returns(CreateScore(0.85));

        var logger = mock.Mock<ILogger<ConfidenceAdapter>>();
        var sut = mock.Create<ConfidenceAdapter>();

        await foreach (var _ in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            // Consume
        }

        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Evaluated") && v.ToString()!.Contains("Best match")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ItWillLogWarningWhenNoCandidatesFound()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults()); // Empty

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        var logger = mock.Mock<ILogger<ConfidenceAdapter>>();
        var sut = mock.Create<ConfidenceAdapter>();

        await foreach (var _ in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            // Should yield nothing
        }

        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No candidates found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Obsolete FindSongAsync Tests

    [Fact]
    public async Task ItWillReturnBestMatchUrlFromObsoleteFindSongAsync()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var lowMatch = new SongSearchResult("https://spotify.com/track/low",
            new SongMetadata { Title = "L", Artists = ["A"], Album = "A", DurationMs = 180000 });
        var highMatch = new SongSearchResult("https://spotify.com/track/high",
            new SongMetadata { Title = "H", Artists = ["A"], Album = "A", DurationMs = 180000 });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(lowMatch, highMatch));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, lowMatch.FoundMetadata))
            .Returns(CreateScore(0.70));

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, highMatch.FoundMetadata))
            .Returns(CreateScore(0.95));

        var sut = mock.Create<ConfidenceAdapter>();

#pragma warning disable CS0618 // Type or member is obsolete
        var result = await sut.FindSongAsync(sourceMetadata, CancellationToken.None);
#pragma warning restore CS0618

        result.Should().Be("https://spotify.com/track/high"); // Best match
    }

    [Fact]
    public async Task ItWillReturnNullFromObsoleteFindSongAsyncWhenNonePassThreshold()
    {
        using var mock = AutoMock.GetLoose();

        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            DurationMs = 180000
        };

        var lowMatch = new SongSearchResult("https://spotify.com/track/1",
            new SongMetadata { Title = "L", Artists = ["A"], Album = "A", DurationMs = 180000 });

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.FindSongsAsync(sourceMetadata, It.IsAny<CancellationToken>()))
            .Returns(YieldResults(lowMatch));

        mock.Mock<IMusicServiceAdapter>()
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);

        mock.Mock<IConfidenceScoreService>()
            .Setup(x => x.CalculateScore(sourceMetadata, lowMatch.FoundMetadata))
            .Returns(CreateScore(0.40));

        var sut = mock.Create<ConfidenceAdapter>();

#pragma warning disable CS0618
        var result = await sut.FindSongAsync(sourceMetadata, CancellationToken.None);
#pragma warning restore CS0618

        result.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private static async IAsyncEnumerable<SongSearchResult> YieldResults(params SongSearchResult[] results)
    {
        foreach (var result in results)
        {
            await Task.Yield();
            yield return result;
        }
    }

    private static async IAsyncEnumerable<SongSearchResult> ThrowAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("Inner adapter failed");
#pragma warning disable CS0162 // Unreachable code detected
        yield break;
#pragma warning restore CS0162
    }

    private static ConfidenceScore CreateScore(double totalScore)
    {
        return new ConfidenceScore
        {
            TotalScore = totalScore,
            TitleScore = totalScore,
            ArtistScore = totalScore,
            AlbumScore = totalScore,
            DurationScore = totalScore
        };
    }

    #endregion
}
