using MusicShare.Contracts;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;
using MusicShare.Services.Services;
using MusicShare.Services.Services.Music;
using Microsoft.Extensions.Logging;

namespace MusicShare.Tests.Unit.Services.Music;

public class ConfidenceAdapterTests
{
    private static ConfidenceAdapter CreateSut(IMusicServiceAdapter innerAdapter, IConfidenceScoreService scoringService, double threshold = 0.65)
        => new(innerAdapter, scoringService, threshold);

    private static ConfidenceAdapter CreateSut(IMusicServiceAdapter innerAdapter)
        => new(innerAdapter, Mock.Of<IConfidenceScoreService>(), 0.65);

    #region Basic Filtering Tests

    [Fact]
    public async Task ItWillReturnHighestConfidenceMatch()
    {
        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var lowMatch = new SongSearchResult(
            "https://spotify.com/track/low",
            new SongMetadata
            {
                Title = "Similar Song",
                Artists = ["Test Artist"],
                Album = "Test Album",
                Duration = TimeSpan.FromMilliseconds(180000)
            });

        var highMatch = new SongSearchResult(
            "https://spotify.com/track/high",
            new SongMetadata
            {
                Title = "Test Song",
                Artists = ["Test Artist"],
                Album = "Test Album",
                Duration = TimeSpan.FromMilliseconds(180000)
            });

        var mediumMatch = new SongSearchResult(
            "https://spotify.com/track/medium",
            new SongMetadata
            {
                Title = "Test Song",
                Artists = ["Different Artist"],
                Album = "Test Album",
                Duration = TimeSpan.FromMilliseconds(180000)
            });

        var lowScore = CreateScore(0.70);
        var highScore = CreateScore(0.95);
        var mediumScore = CreateScore(0.75);

        // Setup inner adapter mock
        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);
        innerAdapter
            .Setup(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(() => YieldResults(lowMatch, mediumMatch, highMatch));

        // Setup scoring service mock
        var scoringService = new Mock<IConfidenceScoreService>();
        scoringService
            .Setup(x => x.CalculateScore(It.IsAny<SongMetadata>(), It.IsAny<SongMetadata>()))
            .Returns<SongMetadata, SongMetadata>((source, found) =>
            {
                if (found == lowMatch.FoundMetadata) return lowScore;
                if (found == highMatch.FoundMetadata) return highScore;
                if (found == mediumMatch.FoundMetadata) return mediumScore;
                return CreateScore(0.0);
            });
        scoringService
            .Setup(x => x.MeetsThreshold(It.IsAny<ConfidenceScore>(), It.IsAny<double>()))
            .Returns<ConfidenceScore, double>((score, t) => score.TotalScore >= t);

        var sut = CreateSut(innerAdapter.Object, scoringService.Object);

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
        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var lowMatch = new SongSearchResult(
            "https://spotify.com/track/1",
            new SongMetadata
            {
                Title = "Different",
                Artists = ["Different"],
                Album = "Different",
                Duration = TimeSpan.FromMilliseconds(200000)
            });

        var lowScore = CreateScore(0.50);

        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);
        innerAdapter
            .Setup(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(() => YieldResults(lowMatch));

        var scoringService = new Mock<IConfidenceScoreService>();
        scoringService
            .Setup(x => x.CalculateScore(It.IsAny<SongMetadata>(), It.IsAny<SongMetadata>()))
            .Returns(lowScore);
        scoringService
            .Setup(x => x.MeetsThreshold(It.IsAny<ConfidenceScore>(), It.IsAny<double>()))
            .Returns<ConfidenceScore, double>((score, t) => score.TotalScore >= t);

        var sut = CreateSut(innerAdapter.Object, scoringService.Object);

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
        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var goodMatch = new SongSearchResult(
            "https://spotify.com/track/1",
            new SongMetadata
            {
                Title = "Test Song",
                Artists = ["Test Artist"],
                Album = "Test Album",
                Duration = TimeSpan.FromMilliseconds(180000)
            });

        var goodScore = CreateScore(0.85);

        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);
        innerAdapter
            .Setup(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(() => YieldResults(goodMatch));

        var scoringService = new Mock<IConfidenceScoreService>();
        scoringService
            .Setup(x => x.CalculateScore(It.IsAny<SongMetadata>(), It.IsAny<SongMetadata>()))
            .Returns(goodScore);
        scoringService
            .Setup(x => x.MeetsThreshold(It.IsAny<ConfidenceScore>(), It.IsAny<double>()))
            .Returns<ConfidenceScore, double>((score, t) => score.TotalScore >= t);

        var sut = CreateSut(innerAdapter.Object, scoringService.Object);

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
        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);
        innerAdapter
            .Setup(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(() => YieldResults()); // Empty results

        var sut = CreateSut(innerAdapter.Object);

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
        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var result1 = new SongSearchResult(
            "https://spotify.com/track/1",
            new SongMetadata { Title = "Song 1", Artists = ["Artist 1"], Album = "Album 1", Duration = TimeSpan.FromMilliseconds(180000) });

        var result2 = new SongSearchResult(
            "https://spotify.com/track/2",
            new SongMetadata { Title = "Song 2", Artists = ["Artist 2"], Album = "Album 2", Duration = TimeSpan.FromMilliseconds(180000) });

        var result3 = new SongSearchResult(
            "https://spotify.com/track/3",
            new SongMetadata { Title = "Song 3", Artists = ["Artist 3"], Album = "Album 3", Duration = TimeSpan.FromMilliseconds(180000) });

        var score = CreateScore(0.85);

        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);
        innerAdapter
            .Setup(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(() => YieldResults(result1, result2, result3));

        var scoringService = new Mock<IConfidenceScoreService>();
        scoringService
            .Setup(x => x.CalculateScore(It.IsAny<SongMetadata>(), It.IsAny<SongMetadata>()))
            .Returns(score);
        scoringService
            .Setup(x => x.MeetsThreshold(It.IsAny<ConfidenceScore>(), It.IsAny<double>()))
            .Returns<ConfidenceScore, double>((s, t) => s.TotalScore >= t);

        var sut = CreateSut(innerAdapter.Object, scoringService.Object);

        await foreach (var _ in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            // Consume results
        }

        scoringService.Verify(x => x.CalculateScore(It.IsAny<SongMetadata>(), result1.FoundMetadata), Times.Once);
        scoringService.Verify(x => x.CalculateScore(It.IsAny<SongMetadata>(), result2.FoundMetadata), Times.Once);
        scoringService.Verify(x => x.CalculateScore(It.IsAny<SongMetadata>(), result3.FoundMetadata), Times.Once);
    }

    [Fact]
    public async Task ItWillScoreEachCandidateBeforeFiltering()
    {
        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var highScoreResult = new SongSearchResult(
            "https://spotify.com/track/high",
            new SongMetadata { Title = "High", Artists = ["Artist"], Album = "Album", Duration = TimeSpan.FromMilliseconds(180000) });

        var lowScoreResult = new SongSearchResult(
            "https://spotify.com/track/low",
            new SongMetadata { Title = "Low", Artists = ["Artist"], Album = "Album", Duration = TimeSpan.FromMilliseconds(180000) });

        var lowScore = CreateScore(0.40);
        var highScore = CreateScore(0.90);

        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);
        innerAdapter
            .Setup(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(() => YieldResults(lowScoreResult, highScoreResult));

        var scoringService = new Mock<IConfidenceScoreService>();
        scoringService
            .Setup(x => x.CalculateScore(It.IsAny<SongMetadata>(), It.IsAny<SongMetadata>()))
            .Returns<SongMetadata, SongMetadata>((source, found) =>
            {
                if (found == lowScoreResult.FoundMetadata) return lowScore;
                if (found == highScoreResult.FoundMetadata) return highScore;
                return CreateScore(0.0);
            });
        scoringService
            .Setup(x => x.MeetsThreshold(It.IsAny<ConfidenceScore>(), It.IsAny<double>()))
            .Returns<ConfidenceScore, double>((score, t) => score.TotalScore >= t);

        var sut = CreateSut(innerAdapter.Object, scoringService.Object);

        var results = new List<SongSearchResult>();
        await foreach (var result in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            results.Add(result);
        }

        // Both should be scored even though one is filtered out
        scoringService.Verify(x => x.CalculateScore(It.IsAny<SongMetadata>(), lowScoreResult.FoundMetadata), Times.Once);
        scoringService.Verify(x => x.CalculateScore(It.IsAny<SongMetadata>(), highScoreResult.FoundMetadata), Times.Once);

        // Only high score should be returned
        results.Should().ContainSingle();
        results[0].Url.Should().Be("https://spotify.com/track/high");
    }

    [Fact]
    public async Task ItWillSortResultsByConfidenceDescending()
    {
        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var result1 = new SongSearchResult("https://spotify.com/track/1",
            new SongMetadata { Title = "1", Artists = ["A"], Album = "A", Duration = TimeSpan.FromMilliseconds(180000) });
        var result2 = new SongSearchResult("https://spotify.com/track/2",
            new SongMetadata { Title = "2", Artists = ["A"], Album = "A", Duration = TimeSpan.FromMilliseconds(180000) });
        var result3 = new SongSearchResult("https://spotify.com/track/3",
            new SongMetadata { Title = "3", Artists = ["A"], Album = "A", Duration = TimeSpan.FromMilliseconds(180000) });

        var score1 = CreateScore(0.95); // Highest
        var score2 = CreateScore(0.70); // Lowest
        var score3 = CreateScore(0.85); // Middle

        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);
        // Return in arbitrary order
        innerAdapter
            .Setup(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(() => YieldResults(result2, result1, result3));

        var scoringService = new Mock<IConfidenceScoreService>();
        // Assign scores based on metadata
        scoringService
            .Setup(x => x.CalculateScore(It.IsAny<SongMetadata>(), It.IsAny<SongMetadata>()))
            .Returns<SongMetadata, SongMetadata>((source, found) =>
            {
                if (found == result1.FoundMetadata) return score1;
                if (found == result2.FoundMetadata) return score2;
                if (found == result3.FoundMetadata) return score3;
                return CreateScore(0.0);
            });
        scoringService
            .Setup(x => x.MeetsThreshold(It.IsAny<ConfidenceScore>(), It.IsAny<double>()))
            .Returns<ConfidenceScore, double>((score, t) => score.TotalScore >= t);

        var sut = CreateSut(innerAdapter.Object, scoringService.Object);

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
        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var high = new SongSearchResult("https://spotify.com/track/high",
            new SongMetadata { Title = "H", Artists = ["A"], Album = "A", Duration = TimeSpan.FromMilliseconds(180000) });
        var medium = new SongSearchResult("https://spotify.com/track/medium",
            new SongMetadata { Title = "M", Artists = ["A"], Album = "A", Duration = TimeSpan.FromMilliseconds(180000) });
        var low = new SongSearchResult("https://spotify.com/track/low",
            new SongMetadata { Title = "L", Artists = ["A"], Album = "A", Duration = TimeSpan.FromMilliseconds(180000) });

        var highScore = CreateScore(0.90);
        var mediumScore = CreateScore(0.70);
        var lowScore = CreateScore(0.45);

        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);
        innerAdapter
            .Setup(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(() => YieldResults(high, medium, low));

        var scoringService = new Mock<IConfidenceScoreService>();
        scoringService
            .Setup(x => x.CalculateScore(It.IsAny<SongMetadata>(), It.IsAny<SongMetadata>()))
            .Returns<SongMetadata, SongMetadata>((source, found) =>
            {
                if (found == high.FoundMetadata) return highScore;
                if (found == medium.FoundMetadata) return mediumScore;
                if (found == low.FoundMetadata) return lowScore;
                return CreateScore(0.0);
            });
        scoringService
            .Setup(x => x.MeetsThreshold(It.IsAny<ConfidenceScore>(), It.IsAny<double>()))
            .Returns<ConfidenceScore, double>((score, t) => score.TotalScore >= t);

        var sut = CreateSut(innerAdapter.Object, scoringService.Object);

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
        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var exactThreshold = new SongSearchResult("https://spotify.com/track/exact",
            new SongMetadata { Title = "T", Artists = ["A"], Album = "A", Duration = TimeSpan.FromMilliseconds(180000) });
        var justBelow = new SongSearchResult("https://spotify.com/track/below",
            new SongMetadata { Title = "T", Artists = ["A"], Album = "A", Duration = TimeSpan.FromMilliseconds(180000) });

        var exactScore = CreateScore(0.65);
        var belowScore = CreateScore(0.649);

        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);
        innerAdapter
            .Setup(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(() => YieldResults(exactThreshold, justBelow));

        var scoringService = new Mock<IConfidenceScoreService>();
        scoringService
            .Setup(x => x.CalculateScore(It.IsAny<SongMetadata>(), It.IsAny<SongMetadata>()))
            .Returns<SongMetadata, SongMetadata>((source, found) =>
            {
                if (found == exactThreshold.FoundMetadata) return exactScore;
                if (found == justBelow.FoundMetadata) return belowScore;
                return CreateScore(0.0);
            });
        scoringService
            .Setup(x => x.MeetsThreshold(It.IsAny<ConfidenceScore>(), It.IsAny<double>()))
            .Returns<ConfidenceScore, double>((score, t) => score.TotalScore >= t);

        var sut = CreateSut(innerAdapter.Object, scoringService.Object);

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
        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var mediumScore = new SongSearchResult("https://spotify.com/track/medium",
            new SongMetadata { Title = "T", Artists = ["A"], Album = "A", Duration = TimeSpan.FromMilliseconds(180000) });

        var score = CreateScore(0.70); // Above default 0.65, below custom 0.80

        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);
        innerAdapter
            .Setup(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(() => YieldResults(mediumScore));

        var scoringService = new Mock<IConfidenceScoreService>();
        scoringService
            .Setup(x => x.CalculateScore(It.IsAny<SongMetadata>(), It.IsAny<SongMetadata>()))
            .Returns(score);
        // Mock MeetsThreshold to return false for custom threshold of 0.80
        scoringService
            .Setup(x => x.MeetsThreshold(It.IsAny<ConfidenceScore>(), It.IsAny<double>()))
            .Returns<ConfidenceScore, double>((s, t) => s.TotalScore >= t);

        var sut = CreateSut(innerAdapter.Object, scoringService.Object, threshold: 0.80);

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
        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var justBelow = new SongSearchResult("https://spotify.com/track/1",
            new SongMetadata { Title = "T", Artists = ["A"], Album = "A", Duration = TimeSpan.FromMilliseconds(180000) });

        var belowScore = CreateScore(0.64);

        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);
        innerAdapter
            .Setup(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(() => YieldResults(justBelow));

        var scoringService = new Mock<IConfidenceScoreService>();
        scoringService
            .Setup(x => x.CalculateScore(It.IsAny<SongMetadata>(), It.IsAny<SongMetadata>()))
            .Returns(belowScore);
        scoringService
            .Setup(x => x.MeetsThreshold(It.IsAny<ConfidenceScore>(), It.IsAny<double>()))
            .Returns<ConfidenceScore, double>((score, t) => score.TotalScore >= t);

        var sut = CreateSut(innerAdapter.Object, scoringService.Object);

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
        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);
        innerAdapter
            .Setup(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(() => YieldResults());

        var sut = CreateSut(innerAdapter.Object);

        await foreach (var _ in sut.FindSongsAsync(sourceMetadata, CancellationToken.None))
        {
            // Consume
        }

        innerAdapter.Verify(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ItWillDelegateDependentMethodsToInnerAdapter()
    {
        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter.Setup(x => x.ServiceType).Returns(ServiceType.AppleMusic);
        innerAdapter.Setup(x => x.NormalizeUrl("test-url")).Returns("normalized-url");
        innerAdapter.Setup(x => x.ExtractSongId("test-url")).Returns("song-123");

        var sut = CreateSut(innerAdapter.Object);

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
        var sourceMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ServiceType)
            .Returns(ServiceType.Spotify);
        innerAdapter
            .Setup(x => x.FindSongsAsync(It.IsAny<SongMetadata>(), It.IsAny<CancellationToken>()))
            .Returns(() => ThrowAsync());

        var sut = CreateSut(innerAdapter.Object);

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
        var expectedMetadata = new SongMetadata
        {
            Title = "Test Song",
            Artists = ["Test Artist"],
            Album = "Test Album",
            Duration = TimeSpan.FromMilliseconds(180000)
        };

        var innerAdapter = new Mock<IMusicServiceAdapter>();
        innerAdapter
            .Setup(x => x.ResolveMetadataAsync("test-url", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetadata);

        var sut = CreateSut(innerAdapter.Object);

        var result = await sut.ResolveMetadataAsync("test-url", CancellationToken.None);

        result.Should().Be(expectedMetadata);
        innerAdapter.Verify(x => x.ResolveMetadataAsync("test-url", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Logging Tests
    // NOTE: Logging tests removed as the simplified ConfidenceAdapter implementation
    // no longer includes logging logic. The adapter now uses LINQ-style filtering
    // and delegates all logging to the inner adapter if needed.
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
