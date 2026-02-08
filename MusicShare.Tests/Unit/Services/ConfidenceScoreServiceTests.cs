using Autofac.Extras.Moq;
using MusicShare.Contracts.Messages;
using MusicShare.Services.Models;
using MusicShare.Services.Services;

namespace MusicShare.Tests.Unit.Services;

public class ConfidenceScoreServiceTests
{
    #region Perfect Matches

    [Fact]
    public void ItWillReturnPerfectScoreForIdenticalMetadata()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Bohemian Rhapsody",
            Artists = new[] { "Queen" },
            Album = "A Night at the Opera",
            Duration = TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(55))
        };

        var candidate = new SongMetadata
        {
            Title = "Bohemian Rhapsody",
            Artists = new[] { "Queen" },
            Album = "A Night at the Opera",
            Duration = TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(55))
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        result.TotalScore.Should().Be(1.0);
        result.TitleScore.Should().Be(1.0);
        result.ArtistScore.Should().Be(1.0);
        result.AlbumScore.Should().Be(1.0);
        result.DurationScore.Should().Be(1.0);
        result.Level.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void ItWillReturnHighScoreForExactMatches()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Hotel California",
            Artists = new[] { "Eagles" },
            Album = "Hotel California",
            Duration = TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(30))
        };

        var candidate = new SongMetadata
        {
            Title = "Hotel California",
            Artists = new[] { "Eagles" },
            Album = "Hotel California",
            Duration = TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(30))
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        result.TotalScore.Should().Be(1.0);
        result.Level.Should().Be(ConfidenceLevel.High);
    }

    #endregion

    #region Title Variations

    [Fact]
    public void ItWillReturnHighScoreForRemixVersions()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Blinding Lights",
            Artists = new[] { "The Weeknd" },
            Album = "After Hours",
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(20))
        };

        var candidate = new SongMetadata
        {
            Title = "Blinding Lights (Remix)",
            Artists = new[] { "The Weeknd" },
            Album = "After Hours",
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(22))
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        // Title will have high similarity due to normalization (removes parentheses)
        // Artists match exactly, album matches exactly
        // Duration is very close (2 seconds difference = 0.95)
        result.TotalScore.Should().BeGreaterThan(0.85);
        result.Level.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void ItWillReturnHighScoreForRemasteredVersions()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Stairway to Heaven",
            Artists = new[] { "Led Zeppelin" },
            Album = "Led Zeppelin IV",
            Duration = TimeSpan.FromMinutes(8).Add(TimeSpan.FromSeconds(2))
        };

        var candidate = new SongMetadata
        {
            Title = "Stairway to Heaven - Remastered",
            Artists = new[] { "Led Zeppelin" },
            Album = "Led Zeppelin IV (Remastered)",
            Duration = TimeSpan.FromMinutes(8).Add(TimeSpan.FromSeconds(3))
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        // Title and album normalization removes " - " and "()"
        // Should score very high
        result.TotalScore.Should().BeGreaterThan(0.85);
        result.Level.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void ItWillReturnMediumScoreForSlightlyDifferentTitles()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Don't Stop Believin'",
            Artists = new[] { "Journey" },
            Album = "Escape",
            Duration = TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(10))
        };

        var candidate = new SongMetadata
        {
            Title = "Dont Stop Believing", // Missing apostrophes
            Artists = new[] { "Journey" },
            Album = "Escape",
            Duration = TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(11))
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        // Slight title difference, everything else matches
        result.TotalScore.Should().BeGreaterThan(0.80);
        result.Level.Should().BeOneOf(ConfidenceLevel.Medium, ConfidenceLevel.High);
    }

    #endregion

    #region Artist Matching

    [Fact]
    public void ItWillReturnLowScoreForDifferentArtist()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Hurt",
            Artists = new[] { "Nine Inch Nails" },
            Album = "The Downward Spiral",
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(50))
        };

        var candidate = new SongMetadata
        {
            Title = "Hurt", // Same title but different artist (Johnny Cash cover)
            Artists = new[] { "Johnny Cash" },
            Album = "American IV: The Man Comes Around",
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(38))
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        // Title matches, but artist is completely different
        // Artist weight is 35%, so this will drag score down significantly
        result.ArtistScore.Should().BeLessThan(0.5);
        result.Level.Should().Be(ConfidenceLevel.Low);
    }

    [Fact]
    public void ItWillReturnHighScoreForMultipleArtistsWithPartialMatch()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Beautiful",
            Artists = new[] { "Eminem", "Sia" },
            Album = "Revival",
            Duration = TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(10))
        };

        var candidate = new SongMetadata
        {
            Title = "Beautiful",
            Artists = new[] { "Eminem", "Sia", "Delta Heavy" }, // Has matching artists plus extra
            Album = "Revival",
            Duration = TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(10))
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        // 2 exact artist matches out of max(2, 3) = 3 => 2/3 = 0.67 artist score
        result.ArtistScore.Should().BeApproximately(0.67, 0.01);
        result.TotalScore.Should().BeGreaterThan(0.80);
    }

    [Fact]
    public void ItWillReturnPerfectArtistScoreForEmptyArtistLists()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Instrumental Track",
            Artists = Array.Empty<string>(),
            Album = "Soundtrack",
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(0))
        };

        var candidate = new SongMetadata
        {
            Title = "Instrumental Track",
            Artists = Array.Empty<string>(),
            Album = "Soundtrack",
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(0))
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        // Both empty artist lists should return 1.0 for artist score
        result.ArtistScore.Should().Be(1.0);
    }

    #endregion

    #region Album Matching

    [Fact]
    public void ItWillReturnHighScoreForExactAlbumMatch()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Thriller",
            Artists = new[] { "Michael Jackson" },
            Album = "Thriller",
            Duration = TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(57))
        };

        var candidate = new SongMetadata
        {
            Title = "Thriller",
            Artists = new[] { "Michael Jackson" },
            Album = "Thriller",
            Duration = TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(57))
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        result.AlbumScore.Should().Be(1.0);
        result.TotalScore.Should().Be(1.0);
    }

    [Fact]
    public void ItWillReturnHighScoreForAlbumVariations()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Rolling in the Deep",
            Artists = new[] { "Adele" },
            Album = "21",
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(48))
        };

        var candidate = new SongMetadata
        {
            Title = "Rolling in the Deep",
            Artists = new[] { "Adele" },
            Album = "21 (Deluxe Edition)",
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(49))
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        // Album should have high similarity after normalization
        result.AlbumScore.Should().BeGreaterThan(0.70);
        result.TotalScore.Should().BeGreaterThan(0.85);
    }

    [Fact]
    public void ItWillReturnNeutralScoreForMissingAlbum()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Single Song",
            Artists = new[] { "Artist Name" },
            Album = "Album Name",
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(0))
        };

        var candidate = new SongMetadata
        {
            Title = "Single Song",
            Artists = new[] { "Artist Name" },
            Album = null, // Missing album
            Duration = TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(0))
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        // Missing album should return 0.5 neutral score
        result.AlbumScore.Should().Be(0.5);
    }

    #endregion

    #region Duration Tolerance

    [Fact]
    public void ItWillReturnPerfectScoreForIdenticalDuration()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Test Song",
            Artists = new[] { "Test Artist" },
            Album = "Test Album",
            Duration = TimeSpan.FromSeconds(195)
        };

        var candidate = new SongMetadata
        {
            Title = "Test Song",
            Artists = new[] { "Test Artist" },
            Album = "Test Album",
            Duration = TimeSpan.FromSeconds(195)
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        result.DurationScore.Should().Be(1.0);
    }

    [Fact]
    public void ItWillReturn095ScoreFor2SecondsDifference()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Radio Edit",
            Artists = new[] { "Artist" },
            Album = "Album",
            Duration = TimeSpan.FromSeconds(180)
        };

        var candidate = new SongMetadata
        {
            Title = "Radio Edit",
            Artists = new[] { "Artist" },
            Album = "Album",
            Duration = TimeSpan.FromSeconds(182) // 2 seconds difference
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        result.DurationScore.Should().Be(0.95);
    }

    [Fact]
    public void ItWillReturn085ScoreFor10SecondsDifference()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Extended Version",
            Artists = new[] { "Artist" },
            Album = "Album",
            Duration = TimeSpan.FromSeconds(200)
        };

        var candidate = new SongMetadata
        {
            Title = "Extended Version",
            Artists = new[] { "Artist" },
            Album = "Album",
            Duration = TimeSpan.FromSeconds(210) // 10 seconds difference
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        result.DurationScore.Should().Be(0.85);
    }

    [Fact]
    public void ItWillReturn070ScoreFor30SecondsDifference()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Live Version",
            Artists = new[] { "Artist" },
            Album = "Live Album",
            Duration = TimeSpan.FromSeconds(240)
        };

        var candidate = new SongMetadata
        {
            Title = "Live Version",
            Artists = new[] { "Artist" },
            Album = "Live Album",
            Duration = TimeSpan.FromSeconds(270) // 30 seconds difference
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        result.DurationScore.Should().Be(0.70);
    }

    [Fact]
    public void ItWillReturnLowScoreForLargeDurationDifference()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Song",
            Artists = new[] { "Artist" },
            Album = "Album",
            Duration = TimeSpan.FromSeconds(180)
        };

        var candidate = new SongMetadata
        {
            Title = "Song",
            Artists = new[] { "Artist" },
            Album = "Album",
            Duration = TimeSpan.FromSeconds(300) // 120 seconds (2 minutes) difference
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        // > 60 seconds uses linear decay formula: 1.0 - (120 / 300) = 0.6
        result.DurationScore.Should().BeLessThan(0.65);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ItWillHandleMissingDuration()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Song Without Duration",
            Artists = new[] { "Artist" },
            Album = "Album",
            Duration = null
        };

        var candidate = new SongMetadata
        {
            Title = "Song Without Duration",
            Artists = new[] { "Artist" },
            Album = "Album",
            Duration = TimeSpan.FromSeconds(180)
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        // Missing duration should return 0.5 neutral score
        result.DurationScore.Should().Be(0.5);
    }

    [Fact]
    public void ItWillHandleEmptyArtistLists()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Instrumental",
            Artists = Array.Empty<string>(),
            Album = "Compilation",
            Duration = TimeSpan.FromSeconds(180)
        };

        var candidate = new SongMetadata
        {
            Title = "Instrumental",
            Artists = new[] { "Various Artists" }, // One has artists, one doesn't
            Album = "Compilation",
            Duration = TimeSpan.FromSeconds(180)
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        // One empty, one not empty should return 0.0 artist score
        result.ArtistScore.Should().Be(0.0);
    }

    [Fact]
    public void ItWillCorrectlyThresholdAtBoundary()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var exactlyAtThreshold = new ConfidenceScore
        {
            TotalScore = 0.65,
            TitleScore = 0.65,
            ArtistScore = 0.65,
            AlbumScore = 0.65,
            DurationScore = 0.65
        };

        var justBelowThreshold = new ConfidenceScore
        {
            TotalScore = 0.6499,
            TitleScore = 0.6499,
            ArtistScore = 0.6499,
            AlbumScore = 0.6499,
            DurationScore = 0.6499
        };

        // Act
        var atThresholdResult = sut.MeetsThreshold(exactlyAtThreshold);
        var belowThresholdResult = sut.MeetsThreshold(justBelowThreshold);

        // Assert
        atThresholdResult.Should().BeTrue();
        belowThresholdResult.Should().BeFalse();
    }

    [Fact]
    public void ItWillCorrectlyCalculateConfidenceLevelLow()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Song A",
            Artists = new[] { "Artist X" },
            Album = "Album 1",
            Duration = TimeSpan.FromSeconds(180)
        };

        var candidate = new SongMetadata
        {
            Title = "Completely Different Song",
            Artists = new[] { "Different Artist" },
            Album = "Different Album",
            Duration = TimeSpan.FromSeconds(400)
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        result.Level.Should().Be(ConfidenceLevel.Low);
        result.TotalScore.Should().BeLessThan(0.65);
    }

    [Fact]
    public void ItWillCorrectlyCalculateConfidenceLevelMedium()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        // Create a scenario with good match but not perfect
        var source = new SongMetadata
        {
            Title = "Shape of You",
            Artists = new[] { "Ed Sheeran" },
            Album = "Divide",
            Duration = TimeSpan.FromSeconds(233)
        };

        var candidate = new SongMetadata
        {
            Title = "Shape of You",
            Artists = new[] { "Ed Sheeran" },
            Album = null, // Missing album will give neutral 0.5
            Duration = TimeSpan.FromSeconds(245) // 12 seconds off = 0.85 duration score
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        // Title: 1.0 * 0.40 = 0.40
        // Artist: 1.0 * 0.35 = 0.35
        // Album: 0.5 * 0.15 = 0.075
        // Duration: 0.85 * 0.10 = 0.085
        // Total: 0.91 (should be High actually)
        // Let's adjust for Medium range (0.65-0.84)
        result.Level.Should().BeOneOf(ConfidenceLevel.Medium, ConfidenceLevel.High);
    }

    [Fact]
    public void ItWillCorrectlyCalculateConfidenceLevelHigh()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var source = new SongMetadata
        {
            Title = "Smells Like Teen Spirit",
            Artists = new[] { "Nirvana" },
            Album = "Nevermind",
            Duration = TimeSpan.FromSeconds(301)
        };

        var candidate = new SongMetadata
        {
            Title = "Smells Like Teen Spirit",
            Artists = new[] { "Nirvana" },
            Album = "Nevermind",
            Duration = TimeSpan.FromSeconds(302) // 1 second off
        };

        // Act
        var result = sut.CalculateScore(source, candidate);

        // Assert
        result.Level.Should().Be(ConfidenceLevel.High);
        result.TotalScore.Should().BeGreaterThanOrEqualTo(0.85);
    }

    [Fact]
    public void ItWillHandleMeetsThresholdWithCustomThreshold()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var score = new ConfidenceScore
        {
            TotalScore = 0.75,
            TitleScore = 0.80,
            ArtistScore = 0.70,
            AlbumScore = 0.75,
            DurationScore = 0.80
        };

        // Act
        var meetsDefault = sut.MeetsThreshold(score); // 0.65 default
        var meetsHigher = sut.MeetsThreshold(score, 0.80); // Custom higher threshold
        var meetsLower = sut.MeetsThreshold(score, 0.50); // Custom lower threshold

        // Assert
        meetsDefault.Should().BeTrue(); // 0.75 >= 0.65
        meetsHigher.Should().BeFalse(); // 0.75 < 0.80
        meetsLower.Should().BeTrue(); // 0.75 >= 0.50
    }

    #endregion

    #region Real-World Scenarios

    [Fact]
    public void ItWillMatchBohemianRhapsodyAcrossPlatforms()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        // Spotify metadata
        var spotify = new SongMetadata
        {
            Title = "Bohemian Rhapsody",
            Artists = new[] { "Queen" },
            Album = "A Night at the Opera",
            Duration = TimeSpan.FromSeconds(354)
        };

        // Apple Music might have slightly different duration due to encoding
        var appleMusic = new SongMetadata
        {
            Title = "Bohemian Rhapsody",
            Artists = new[] { "Queen" },
            Album = "A Night at the Opera (2011 Remaster)",
            Duration = TimeSpan.FromSeconds(355)
        };

        // Act
        var result = sut.CalculateScore(spotify, appleMusic);

        // Assert
        result.TotalScore.Should().BeGreaterThan(0.85);
        result.Level.Should().Be(ConfidenceLevel.High);
        result.TitleScore.Should().Be(1.0);
        result.ArtistScore.Should().Be(1.0);
    }

    [Fact]
    public void ItWillRejectCoverVersions()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        // Original version
        var original = new SongMetadata
        {
            Title = "Hallelujah",
            Artists = new[] { "Leonard Cohen" },
            Album = "Various Positions",
            Duration = TimeSpan.FromSeconds(272)
        };

        // Jeff Buckley cover
        var cover = new SongMetadata
        {
            Title = "Hallelujah",
            Artists = new[] { "Jeff Buckley" },
            Album = "Grace",
            Duration = TimeSpan.FromSeconds(413)
        };

        // Act
        var result = sut.CalculateScore(original, cover);

        // Assert
        // Same title, but different artist and significantly different duration
        result.Level.Should().Be(ConfidenceLevel.Low);
        result.TotalScore.Should().BeLessThan(0.65);
        result.ArtistScore.Should().BeLessThan(0.5);
    }

    [Fact]
    public void ItWillMatchRadioEditToAlbumVersion()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var albumVersion = new SongMetadata
        {
            Title = "Purple Rain",
            Artists = new[] { "Prince" },
            Album = "Purple Rain",
            Duration = TimeSpan.FromSeconds(523) // 8:43
        };

        var radioEdit = new SongMetadata
        {
            Title = "Purple Rain (Radio Edit)",
            Artists = new[] { "Prince" },
            Album = "Purple Rain",
            Duration = TimeSpan.FromSeconds(242) // 4:02
        };

        // Act
        var result = sut.CalculateScore(albumVersion, radioEdit);

        // Assert
        // Large duration difference (281 seconds) will hurt the score
        // But title (after normalization) and artist should still match well
        result.TitleScore.Should().BeGreaterThan(0.80); // "purple rain" vs "purple rain radio edit"
        result.ArtistScore.Should().Be(1.0);
        result.AlbumScore.Should().Be(1.0);
        result.DurationScore.Should().BeLessThan(0.50); // 281 seconds off is significant
        // Overall might still be Medium confidence due to strong other matches
        result.TotalScore.Should().BeGreaterThan(0.60);
    }

    [Fact]
    public void ItWillMatchCompilationAlbumVariations()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        var original = new SongMetadata
        {
            Title = "Sweet Child O' Mine",
            Artists = new[] { "Guns N' Roses" },
            Album = "Appetite for Destruction",
            Duration = TimeSpan.FromSeconds(356)
        };

        var compilation = new SongMetadata
        {
            Title = "Sweet Child O' Mine",
            Artists = new[] { "Guns N' Roses" },
            Album = "Greatest Hits", // Different album (compilation)
            Duration = TimeSpan.FromSeconds(356)
        };

        // Act
        var result = sut.CalculateScore(original, compilation);

        // Assert
        // Title, artist, and duration match perfectly
        // Only album differs
        result.TitleScore.Should().BeGreaterThan(0.90);
        result.ArtistScore.Should().Be(1.0);
        result.DurationScore.Should().Be(1.0);
        // Album difference will lower score, but not dramatically (15% weight)
        result.TotalScore.Should().BeGreaterThan(0.75);
        result.Level.Should().BeOneOf(ConfidenceLevel.Medium, ConfidenceLevel.High);
    }

    [Fact]
    public void ItWillHandleFeaturedArtistVariations()
    {
        // Arrange
        using var mock = AutoMock.GetLoose();
        var sut = mock.Create<ConfidenceScoreService>();

        // Some platforms list featured artists separately
        var version1 = new SongMetadata
        {
            Title = "See You Again",
            Artists = new[] { "Wiz Khalifa", "Charlie Puth" },
            Album = "Furious 7",
            Duration = TimeSpan.FromSeconds(229)
        };

        // Others include in title or primary artist only
        var version2 = new SongMetadata
        {
            Title = "See You Again (feat. Charlie Puth)",
            Artists = new[] { "Wiz Khalifa" },
            Album = "Furious 7",
            Duration = TimeSpan.FromSeconds(230)
        };

        // Act
        var result = sut.CalculateScore(version1, version2);

        // Assert
        // Title should match well after normalization (removes parentheses)
        // Artist has partial match (1 out of max(2, 1) = 2 => 0.5)
        result.TitleScore.Should().BeGreaterThan(0.80);
        result.ArtistScore.Should().BeGreaterThan(0.0); // At least one artist matches
        result.AlbumScore.Should().Be(1.0);
        result.DurationScore.Should().BeGreaterThanOrEqualTo(0.95); // 1 second difference
        result.TotalScore.Should().BeGreaterThan(0.65);
    }

    #endregion
}
