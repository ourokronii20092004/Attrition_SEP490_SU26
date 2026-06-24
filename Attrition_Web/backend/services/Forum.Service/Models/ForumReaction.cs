namespace Forum.Service.Models;

public class ForumReaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public string ReactionType { get; set; } = Models.ReactionType.Like;        // like | dislike
}
