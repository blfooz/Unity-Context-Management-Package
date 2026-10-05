using ContextManagement;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class NPCFriendship : MonoBehaviour, IContextProvider
{
    public int friendship;

    private void Start()
    {
        friendship = Random.Range(-30, 31);
    }

    public string ContextDescription => "NPC's current friendship level.";

    public void ToolModifyFriendshipLevel(ToolCallContext ctx)
    {
        friendship += int.Parse(ctx["delta"].ToString());
        ctx.Return($"Friendship level is now {friendship} ({FriendshipLevelString(friendship)})");
    }

    public bool TryGetContext(LLMRequestManager caller, out string context)
    {
        context = $"# Friendship Level\n{friendship}/±100 ({FriendshipLevelString(friendship)})";
        return true;
    }

    public static string FriendshipLevelString(int friendship)
    {
        return friendship switch
        {
            > 80 => "Intimate",
            > 60 => "Familiar",
            > 40 => "Friendly",
            > 20 => "Acquaintance",
            < -80 => "Despised",
            < -60 => "Hostile",
            < -40 => "Disliked",
            < -20 => "Unfavored",
            _ => "Stranger"
        };
    }
}
