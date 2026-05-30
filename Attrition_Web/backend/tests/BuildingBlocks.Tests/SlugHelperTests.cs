using BuildingBlocks.Contracts;
using Xunit;

namespace BuildingBlocks.Tests;

public class SlugHelperTests
{
    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("  Trim  Me  ", "trim-me")]
    [InlineData("Special!@#Chars", "specialchars")]
    [InlineData("Multiple   Spaces", "multiple-spaces")]
    public void GenerateSlug_BasicLatin(string input, string expected)
    {
        Assert.Equal(expected, SlugHelper.GenerateSlug(input));
    }

    [Fact]
    public void GenerateSlug_StripsVietnameseDiacritics()
    {
        // Regression: previously produced "nhn-vt" (or empty) by stripping accented chars.
        Assert.Equal("nhan-vat", SlugHelper.GenerateSlug("Nhân vật"));
    }

    [Fact]
    public void GenerateSlug_MapsVietnameseDee()
    {
        Assert.Equal("dao", SlugHelper.GenerateSlug("Đao"));
    }

    [Fact]
    public void GenerateSlug_NonLatinNeverEmpty()
    {
        // CJK has no ASCII transliteration here; must fall back to a stable, non-empty slug.
        var slug = SlugHelper.GenerateSlug("敵キャラ");
        Assert.False(string.IsNullOrEmpty(slug));
        Assert.StartsWith("item-", slug);
        // Stable for the same input.
        Assert.Equal(slug, SlugHelper.GenerateSlug("敵キャラ"));
    }

    [Fact]
    public async Task GenerateUniqueSlugAsync_SuffixesOnCollision()
    {
        var taken = new HashSet<string> { "hello-world", "hello-world-2" };
        var slug = await SlugHelper.GenerateUniqueSlugAsync("Hello World",
            s => Task.FromResult(taken.Contains(s)));
        Assert.Equal("hello-world-3", slug);
    }

    [Fact]
    public async Task GenerateUniqueSlugAsync_NoCollisionReturnsBase()
    {
        var slug = await SlugHelper.GenerateUniqueSlugAsync("Fresh Title",
            _ => Task.FromResult(false));
        Assert.Equal("fresh-title", slug);
    }
}
