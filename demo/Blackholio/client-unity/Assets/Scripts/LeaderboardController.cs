using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public class LeaderboardController : MonoBehaviour
{
	private const int MAX_ROW_COUNT = 11; //10 + local player

    [FormerlySerializedAs("RowPrefab")]
    public LeaderboardRow rowPrefab;
    [FormerlySerializedAs("Root")]
    public Transform root;

    private readonly LeaderboardRow[] rows = new LeaderboardRow[MAX_ROW_COUNT];

    private void Start()
    {
        for (var i = 0; i < MAX_ROW_COUNT; i++)
        {
            var go = Instantiate(rowPrefab, root, true);
            go.gameObject.SetActive(false);
            rows[i] = go;
        }
    }
    
    private void Update()
    {
        var players = GameManager.Players.Values.
	        Select(static a => (player: a, mass: a.TotalMass())).
	        Where(static a => a.mass > 0).
	        OrderByDescending(static a => a.mass).
	        Take(10).
	        ToList();
        var localPlayer = PlayerController.Local;
		if (localPlayer != null && players.All(p => p.player != localPlayer) && localPlayer.NumberOfOwnedCircles > 0)
        {
            players.Add((localPlayer, localPlayer.TotalMass()));
		}

        int i;
        for (i = 0; i < players.Count; i++)
		{
            var player = players[i];
			var row = rows[i];
            row.SetData(player.player.Username, player.mass);
            row.gameObject.SetActive(true);
		}
        for (; i < MAX_ROW_COUNT; i++)
		{
			rows[i].gameObject.SetActive(false);
		}
    }
}