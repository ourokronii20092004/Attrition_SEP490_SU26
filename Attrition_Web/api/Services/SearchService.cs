using Attrition.API.DTOs;
using Attrition.API.Models;
using Attrition.API.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Attrition.API.Services;

public class SearchService : ISearchService
{
    private readonly IWikiRepository _wikiRepo;
    private readonly IUserRepository _userRepo;
    private readonly IRepository<ForumPost> _forumPostRepo;
    private readonly IForumRepository _forumRepo;

    public SearchService(
        IWikiRepository wikiRepo,
        IUserRepository userRepo,
        IRepository<ForumPost> forumPostRepo,
        IForumRepository forumRepo)
    {
        _wikiRepo = wikiRepo;
        _userRepo = userRepo;
        _forumPostRepo = forumPostRepo;
        _forumRepo = forumRepo;
    }

    public async Task<GlobalSearchResponse> GlobalSearchAsync(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return new GlobalSearchResponse(new List<SearchWikiResultDto>(), new List<SearchUserResultDto>(), new List<SearchPostResultDto>());

        q = q.Trim();
        var isWiki = false;
        var isUser = false;
        var isPost = false;

        var tagMatch = Regex.Match(q, @"^(wiki|user|post):\s*""?([^""]+)""?", RegexOptions.IgnoreCase);
        if (tagMatch.Success)
        {
            var tag = tagMatch.Groups[1].Value.ToLower();
            q = tagMatch.Groups[2].Value;
            if (tag == "wiki") isWiki = true;
            else if (tag == "user") isUser = true;
            else if (tag == "post") isPost = true;
        }

        var searchAll = !isWiki && !isUser && !isPost;

        var wikiResults = new List<SearchWikiResultDto>();
        var userResults = new List<SearchUserResultDto>();
        var postResults = new List<SearchPostResultDto>();

        if (searchAll || isWiki)
        {
            var (articles, _) = await _wikiRepo.GetPagedAsync(1, 5, 
                a => EF.Functions.ToTsVector("english", a.Title + " " + a.Content).Matches(EF.Functions.WebSearchToTsQuery("english", q)) ||
                     a.Title.ToLower().Contains(q.ToLower()));
            wikiResults = articles.Select(a => new SearchWikiResultDto(a.Id, a.Title, a.Slug)).ToList();
        }

        if (searchAll || isUser)
        {
            var (users, _) = await _userRepo.GetPagedAsync(1, 5, u => u.Username.Contains(q) || u.DisplayName!.Contains(q));
            userResults = users.Select(u => new SearchUserResultDto(u.Id, u.Username, u.DisplayName, u.AvatarPath, u.GoogleAvatarUrl, u.Role)).ToList();
        }

        if (searchAll || isPost)
        {
            var (posts, _) = await _forumPostRepo.GetPagedAsync(1, 5, 
                p => !p.IsRemoved && (EF.Functions.ToTsVector("english", p.Content).Matches(EF.Functions.WebSearchToTsQuery("english", q)) || p.Content.ToLower().Contains(q.ToLower())));
            
            var (threadsMatching, _) = await _forumRepo.GetPagedAsync(1, 5, 
                t => EF.Functions.ToTsVector("english", t.Title).Matches(EF.Functions.WebSearchToTsQuery("english", q)) || t.Title.ToLower().Contains(q.ToLower()));
            var threadIdsMatching = threadsMatching.Select(t => t.Id).ToList();

            var (postsInThreads, _) = await _forumPostRepo.GetPagedAsync(1, 5, p => !p.IsRemoved && threadIdsMatching.Contains(p.ThreadId));

            var combinedPosts = posts.Concat(postsInThreads).DistinctBy(p => p.Id).Take(5).ToList();

            foreach (var p in combinedPosts)
            {
                var thread = await _forumRepo.GetByIdAsync(p.ThreadId);
                postResults.Add(new SearchPostResultDto(p.Id, p.ThreadId, p.Content, thread?.Title ?? string.Empty, p.CreatedAt));
            }
        }

        return new GlobalSearchResponse(wikiResults, userResults, postResults);
    }
}
