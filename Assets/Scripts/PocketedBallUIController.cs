using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PocketedBallUIController : MonoBehaviour
{
    public enum Team { Neutral, Solids, Stripes }

    [Header("References")]
    public UnityEngine.UI.Image[] ballSlots; // Size = 15, 1-7 Solids, 8 in center, 9-15 Stripes
    public UnityEngine.UI.Image border;
    public Color solidsColor = Color.red;
    public Color stripesColor = Color.yellow;
    public Color eightBallColor = Color.black;

    private bool[] pocketed = new bool[16];
    private Team currentTeam = Team.Neutral;

    public void SetTeam(Team team)
    {
        currentTeam = team;
        switch (team)
        {
            case Team.Solids:
                border.color = new Color(solidsColor.r, solidsColor.g, solidsColor.b, 0.25f);
                break;
            case Team.Stripes:
                border.color = new Color(stripesColor.r, stripesColor.g, stripesColor.b, 0.25f);
                break;
            default:
                border.color = new Color(255f, 255f, 255f, 0.25f);
                break;
        }
    }

    public void ResetUI()
    {
        for (int i = 1; i <= 15; i++)
        {
            pocketed[i] = false;
        }

        foreach (UnityEngine.UI.Image slot in ballSlots)
        {
            slot.color = new Color(0f, 0f, 0f, 0.25f);
        }

        SetTeam(Team.Neutral); // Reset team color
    }

    public void OnBallPocketed(int id)
    {
        if (id < 1 || id > 15 || pocketed[id]) return;
        pocketed[id] = true;

        //int index = GetSlotIndex(id); // If I wanted to use ball indexes instead
        int index = GetNextAvailableSlotIndex(id); 

        if (index < 0 || index >= ballSlots.Length) return;

        if (id == 8)
            ballSlots[index].color = eightBallColor;
        else if (id <= 7)
            ballSlots[index].color = solidsColor;
        else
            ballSlots[index].color = stripesColor;
    }

    private int GetNextAvailableSlotIndex(int id)
    {
        if (id == 8) return 7;

        bool isSolid = id >= 1 && id <= 7;
        bool isStripe = id >= 9 && id <= 15;

        if (isSolid)
        {
            for (int i = 0; i <= 6; i++)
            {
                if (ballSlots[i].color.a < 0.9f) return i;
            }
        }
        else if (isStripe)
        {
            for (int i = 14; i >= 8; i--)
            {
                if (ballSlots[i].color.a < 0.9f) return i;
            }
        }
        return -1; // No available slot
    }

    private int GetSlotIndex(int id)
    {
        if (id == 8) return 7; // 7 (center)
        else if (id >= 1 && id <= 7) return id - 1; // 0-6 (left)
        else if (id >= 9 && id <= 15) return 15 - id + 8; // 8-14 (right)
        else return -1;
    }
}
