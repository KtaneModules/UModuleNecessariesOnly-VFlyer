using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Wawa.DDL;

public class USettings
{
    public int maxUPairs = -1;
}

public class UGlobalHandler {
    public KMBomb usedBombRef;
    public List<UniformScript> uHandlers = new List<UniformScript>();
    public UGlobalHandler(KMBomb referenceBomb = null)
    {
        usedBombRef = referenceBomb;
    }
    public bool CheckIfAllPresent()
    {
        var allLinkedUs = usedBombRef.GetComponentsInChildren<UniformScript>();
        return uHandlers.Count == allLinkedUs.Length;
    }
    bool TryOverrideMissionPairLimit(USettings baseSettings, out int pairCounts)
    {
        try
        {
            //var usedURef = uHandlers.First();
            var missionID = Missions.Id.Value ?? "freeplay";
            switch (missionID)
            {
                default:
                    break;
            }
            var description = Missions.Description.Value ?? "";
            var rgxOverride = Regex.Match(description, @"\[U\]\s\d+");
            pairCounts = baseSettings.maxUPairs;
            var successful = false;
            if (rgxOverride.Success)
            {
                int obtainedValue;
                if (int.TryParse(rgxOverride.Value.Split().Skip(1).First(), out obtainedValue))
                {
                    successful = true;
                    pairCounts = obtainedValue;
                }
            }
            return successful;
        }
        catch
        {
            Debug.LogErrorFormat("An exception has been thrown for attempting to override the module's handler.");
            pairCounts = baseSettings.maxUPairs;
            return false;
        }
    }
    public void HandleGlobalModules()
    {
        if (!CheckIfAllPresent()) return;
        var USettings = new ModConfig<USettings>("USettings").Settings;
        int maxPairsAllowed;
        if (TryOverrideMissionPairLimit(USettings, out maxPairsAllowed))
            Debug.Log("<UGlobalHandler> Override successful.");
        var allUniformModules = uHandlers.ToList().Shuffle();
        var pairsCreated = maxPairsAllowed < 0 ? allUniformModules.Count >> 1 : Mathf.Min(allUniformModules.Count >> 1, maxPairsAllowed);

        for (var x = 0; x < 2 * pairsCreated; x += 2)
        {
            var _1stUMod = allUniformModules[x];
            var _2ndUMod = allUniformModules[x + 1];
            _2ndUMod.SetLocalU(_1stUMod, true);
            _1stUMod.SetLocalU(_2ndUMod);
            var pickedColor = new Color(Random.value, Random.value, Random.value);
            _2ndUMod.pairingColor = pickedColor;
            _1stUMod.pairingColor = pickedColor;
        }
    }
}
