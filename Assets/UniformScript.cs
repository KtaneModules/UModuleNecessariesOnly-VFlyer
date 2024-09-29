using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using Random = UnityEngine.Random;
using System.Text.RegularExpressions;

public class UniformScript : MonoBehaviour {

	public KMAudio mAudio;
	public KMSelectable letterSelectable, pairSelectable;
	public KMBombInfo bombInfo;
	public KMBombModule modSelf;
	public KMGameInfo gameInfo;
	public KMColorblindMode colorblindHandler;
	public MeshRenderer[] colorblindRenderers;
	public MeshRenderer pairRenderer, URenderer, backingRender;
	public Material unlitMat, diffuseMat;
	public Transform affectedAncillery, solveTransform;
	static public Dictionary<KMBomb, UGlobalHandler> allUniformHandlers = new Dictionary<KMBomb, UGlobalHandler>();
	protected UGlobalHandler globalHandler;
	protected UniformScript linkedUModule;
	KMBomb storedBomb;
	protected int[] relevantTimedValues, relevantLogicGates;
	protected bool isShaking = false;
	public int targetValue { get; private set; }
	public int determinedIdx { get; private set; }
	static int modIDCnt;
	public int moduleID { get; private set; }
	private int lastTimeMod60 = -1;
	bool activated = false, moduleSolved = false, requireColorblind = false, hovering = false;
	public Color pairingColor;
	static readonly Color[] allPossibleColors = new Color[] {
		Color.black,
		new Color(0.33f, 0.33f, 0.33f),
		new Color(0, 0, 0.33f),
		Color.blue,
		new Color(0, 0.33f, 0),
		Color.green,
		new Color(0, 0.33f, 0.33f),
		Color.cyan,
		new Color(0.33f, 0, 0),
		Color.red,
		new Color(0.33f, 0, 0.33f),
		Color.magenta,
		new Color(0.33f, 0.33f, 0),
		Color.yellow,
		new Color(0.67f, 0.67f, 0.67f),
		Color.white,
	};
    static readonly int[] colorIdxDetermineGate = new[] { 0, 3, 5, 7, 9, 11, 13, 15 }, colorIdxColorblind = new[] { 9, 5, 3, 15 };
	static readonly string[] logicGates = new[] { "AND", "NIMPBY", "NOR", "NIMP", "OR", "IMP", "NAND", "IMPBY" };
	public void SetLocalU(UniformScript linkedModule, bool alterGates = false)
    {
		linkedUModule = linkedModule;
		if (alterGates)
			linkedUModule.relevantLogicGates = Enumerable.Range(0, 8).Where(a => !relevantLogicGates.Contains(a)).ToArray().Shuffle();
    }
	void HandleUPress()
    {
		if (!activated || moduleSolved) return;
		var timePressed = Mathf.FloorToInt(bombInfo.GetTime());
		QuickLog("This module was pressed when the countdown timer is {0}. Mod 15 seconds is {1}", bombInfo.GetFormattedTime(), timePressed % 15);
		if (timePressed % 15 == targetValue % 15)
        {
			QuickLog("Lines up with the target value. Module disarmed.");
			moduleSolved = true;
			modSelf.HandlePass();
			mAudio.PlaySoundAtTransform("solve_sound_FMods", transform);
			StartCoroutine(SolveAnim());
        }
		else
        {
			QuickLog("This does not line up with the target value. Striking.");
			modSelf.HandleStrike();
		}
		gameInfo.OnLightsChange += HandleLightChange;
    }
	void HandleLightChange(bool lightsOn = false)
    {
		URenderer.material = lightsOn ? diffuseMat : unlitMat;
		pairRenderer.material = lightsOn ? diffuseMat : unlitMat;
		if (requireColorblind)
			for (var x = 0; x < colorblindRenderers.Length; x++)
				colorblindRenderers[x].material = lightsOn ? diffuseMat : unlitMat;
		HandleUpdateTick();
    }
	void Awake()
    {
		allUniformHandlers.Clear();
		try
        {
			requireColorblind = colorblindHandler.ColorblindModeActive;
        }
		catch
        {
			requireColorblind = false;
        }
    }
	// Use this for initialization
	void Start () {
		moduleID = ++modIDCnt;
		targetValue = Random.Range(0, 16);
		// Section of unpair handling.
		relevantLogicGates = Enumerable.Range(0, 8).ToArray().Shuffle().Take(4).ToArray();
		determinedIdx = Random.Range(0, 4);
		switch (Random.Range(0, 4))
        {
			default: // 0 - 14
				relevantTimedValues = Enumerable.Range(0, 15).ToArray();
				break;
			case 0: // 1 - 15 ([0 + 1] - [14 + 1])
				relevantTimedValues = Enumerable.Range(1, 15).ToArray();
				break;
			case 1: // 1 - 15, 15 = 0
				relevantTimedValues = Enumerable.Range(1, 15).Select(a => a % 15).ToArray();
				break;
			case 3: // 1 - 15, 0 = 15
				relevantTimedValues = Enumerable.Range(0, 15).Select(a => a == 0 ? 15 : a).ToArray();
				break;
		}
		StartCoroutine(DelayHandleGlobalHandler());
		modSelf.OnActivate += ActivateModule;
		letterSelectable.OnInteract += delegate {
			HandleUPress();
			return false;
		};
		letterSelectable.OnHighlight += delegate {
			if (moduleSolved || !activated) return;
			colorblindRenderers.Last().enabled = false;
			hovering = true;
			var determinedLogicGate = relevantLogicGates[determinedIdx];

			var binary = Convert.ToString(determinedLogicGate, 2).PadLeft(3, '0');
			for (var x = 0; x < colorblindRenderers.Length - 1; x++)
			{
				colorblindRenderers[x].enabled = requireColorblind;
				colorblindRenderers[x].material.color = binary[x] == '1' ? allPossibleColors[colorIdxColorblind[x]] : Color.black;
			}
			backingRender.material.color = allPossibleColors[colorIdxDetermineGate[determinedLogicGate]];
		};
		letterSelectable.OnHighlightEnded += delegate {
			if (moduleSolved || !activated) return;
			colorblindRenderers.Last().enabled = requireColorblind;
			hovering = false;
			HandleUpdateTick();
			backingRender.material.color = Color.white;
		};

		pairSelectable.OnInteract += delegate {
			if (!isShaking)
			{
				if (moduleSolved && (linkedUModule == null || linkedUModule.moduleSolved)) return false;

				if (linkedUModule != null)
					StartCoroutine(linkedUModule.ShakeModule(true));
				StartCoroutine(ShakeModule());
			}
			return false;
		};
		for (var x = 0; x < colorblindRenderers.Length; x++)
			colorblindRenderers[x].enabled = false;
		affectedAncillery.transform.localRotation *= Quaternion.Euler(0, 90 * determinedIdx, 0);
	}
	void ActivateModule()
    {
		if (linkedUModule != null)
		{
			/*relevantTimedValues = Enumerable.Repeat(linkedUModule.targetValue, 15).ToArray();
			QuickLog("The value used to apply the operator will be {0}, from the linked U module.", linkedUModule.targetValue);*/
			pairRenderer.material.color = pairingColor;
			QuickLog("This module is linked to U #{0}. Logic gates have been shared.", linkedUModule.moduleID);
			pairSelectable.gameObject.SetActive(true);
		}
		else
		{
			QuickLog("This module is not linked to any other U modules.");
			pairSelectable.gameObject.SetActive(false);
		}
		QuickLog("The target value is {0}, which must be submitting when the time remaining in seconds is {1}, modulo 15.", targetValue, targetValue % 15);
		QuickLog("The logic gates used on this module are {0}", relevantLogicGates.Select(a => logicGates[a]).Join(", "));
		QuickLog("The logic gate that can always be determined is when the seconds timer, modulo 4, is {0}", determinedIdx);
		QuickLog("The values used to apply the operator are {0}", relevantTimedValues.Join(", "));
		QuickLog("<table class=\"ULogicTable\"><tr><th>{0}</th></tr><tr>{1}</tr></table>",
			relevantLogicGates.Select(a => logicGates[a]).Join("</th><th>"),
			relevantTimedValues.Select(a => string.Format("<td>{0}</td><th>{1}</th><th>{2}</th>", relevantLogicGates.Select(b => Convert.ToString(EvaulateOperator(targetValue, a, b), 2).PadLeft(4, '0')).Join("</td><td>"), a, Convert.ToString(a, 2).PadLeft(4, '0'))).Join("</tr><tr>")
		);
		activated = true;
    }
	// Global handler for multiple U modules.
	IEnumerator DelayHandleGlobalHandler()
    {
		yield return null;
		storedBomb = transform.GetComponentInParent<KMBomb>();
		if (!allUniformHandlers.ContainsKey(storedBomb))
			allUniformHandlers[storedBomb] = new UGlobalHandler(storedBomb);
		globalHandler = allUniformHandlers[storedBomb];
		globalHandler.uHandlers.Add(this);
		globalHandler.HandleGlobalModules();
		yield break;
    }

	public void QuickLog(string toLog, params object[] args)
	{
		Debug.LogFormat("[{0} #{1}] {2}", modSelf.ModuleDisplayName, moduleID, string.Format(toLog, args));
	}
	protected IEnumerator ShakeModule(bool reversed = false)
    {
		isShaking = true;
		var storedRot = transform.localRotation;
		var affectedRotL = transform.localRotation * Quaternion.Euler(0, -30, 0);
		var affectedRotR = transform.localRotation * Quaternion.Euler(0, 30, 0);
		for (float t = 0; t < 1f; t += Time.deltaTime / 2)
        {
			yield return null;
			transform.localRotation = reversed ? Quaternion.Lerp(affectedRotL, affectedRotR, Easing.InOutSine(t + 0.25f, 0, 1, 0.5f)) : Quaternion.Lerp(affectedRotR, affectedRotL, Easing.InOutSine(t + 0.25f, 0, 1, 0.5f));
        }
		transform.localRotation = storedRot;
		isShaking = false;
	}
	IEnumerator HideCBDisplays()
    {
		var lastScalesCBs = colorblindRenderers.Select(a => a.transform.localScale).ToArray();
		for (var x = 0; x < colorblindRenderers.Length; x++)
			colorblindRenderers[x].material.color = Color.black;
		for (float t = 0; t < 1f; t += Time.deltaTime * 2)
        {
			for (var x = 0; x < colorblindRenderers.Length; x++)
				colorblindRenderers[x].transform.localScale = Vector3.Lerp(lastScalesCBs[x], Vector3.zero, t);
			yield return null;
		}
		for (var x = 0; x < colorblindRenderers.Length; x++)
		{
			colorblindRenderers[x].enabled = false;
			colorblindRenderers[x].transform.localScale = lastScalesCBs[x];
		}
	}
	IEnumerator SolveAnim()
    {
		backingRender.material.color = Color.white;
		URenderer.material.color = Color.green;
		StartCoroutine(RaiseU());
		StartCoroutine(HideCBDisplays());
		var lastTransformAncillery = affectedAncillery.transform.localRotation;
        for (float t = 0; t < 1f; t += Time.deltaTime)
        {
			affectedAncillery.transform.localRotation = Quaternion.Lerp(lastTransformAncillery, Quaternion.Euler(0, 0, 0), t);
			yield return null;
		}
		affectedAncillery.transform.localRotation = Quaternion.Euler(0, 0, 0);
	}
	IEnumerator RaiseU(float speed = 1f)
    {
		var lastPosU = letterSelectable.transform.localPosition;
		var lastScaleU = letterSelectable.transform.localScale;
		var lastRotU = letterSelectable.transform.localRotation;
        var URotated180 = lastRotU * Quaternion.Euler(0, 180, 0);
		for (float t = 0; t < 1f; t += Time.deltaTime * speed)
        {
			letterSelectable.transform.localRotation = Quaternion.Lerp(lastRotU, URotated180, t);
			letterSelectable.transform.localPosition = Vector3.Lerp(lastPosU, solveTransform.localPosition, t);
			letterSelectable.transform.localScale = Vector3.Lerp(lastScaleU, solveTransform.localScale, t);
			yield return null;
		}
		var URotated360 = URotated180 * Quaternion.Euler(0, 180, 0);
		for (float t = 0; t < 1f; t += Time.deltaTime * speed)
		{
			letterSelectable.transform.localRotation = Quaternion.Lerp(URotated180, URotated360, t);
			letterSelectable.transform.localPosition = Vector3.Lerp(solveTransform.localPosition, lastPosU, t);
			letterSelectable.transform.localScale = Vector3.Lerp(solveTransform.localScale, lastScaleU, t);
			yield return null;
		}
		letterSelectable.transform.localRotation = lastRotU;
		letterSelectable.transform.localPosition = lastPosU;
		letterSelectable.transform.localScale = lastScaleU;
	}
	int EvaulateOperator(int valueA, int valueB, int idxOperator, int length = 4)
    {
		var valueAll1sLength = Convert.ToInt32(Enumerable.Repeat('1', length).Join(""), 2);

		switch (idxOperator)
		{
			case 0: return valueA & valueB; // AND
			case 1: return valueA ^ valueAll1sLength & valueB; // NIMPBY
			case 2: return (valueA | valueB) ^ valueAll1sLength; // NOR
			case 3: return valueA & (valueB ^ valueAll1sLength); // NIMP
			case 4: return valueA | valueB; // OR
			case 5: return (valueA ^ valueAll1sLength) | valueB; // IMP
			case 6: return (valueA & valueB) ^ valueAll1sLength; // NAND
			case 7: return valueA | (valueB^ valueAll1sLength); // IMPBY
		}
		return valueA;
	}
	void HandleUpdateTick()
    {
		var displayValIdx = targetValue;
		var valueB = relevantTimedValues[lastTimeMod60 % relevantTimedValues.Length];
		displayValIdx = EvaulateOperator(displayValIdx, valueB, relevantLogicGates[lastTimeMod60 % relevantLogicGates.Length]);
		if (requireColorblind)
		{
			if (!hovering)
			{
				var binary = Convert.ToString(displayValIdx, 2).PadLeft(4, '0');
				for (var x = 0; x < colorblindRenderers.Length; x++)
				{
					colorblindRenderers[x].enabled = true;
					colorblindRenderers[x].material.color = binary[x] == '1' ? allPossibleColors[colorIdxColorblind[x]] : Color.black;
				}
			}
		}
		else
			for (var x = 0; x < colorblindRenderers.Length; x++)
				colorblindRenderers[x].enabled = false;
		URenderer.material.color = allPossibleColors[displayValIdx];
    }
	protected string GetModuleCode()
	{
		Transform closest = null;
		float closestDistance = float.MaxValue;
		foreach (Transform children in transform.parent)
		{
			var distance = (transform.position - children.position).magnitude;
			if (children.gameObject.name == "TwitchModule(Clone)" && (closest == null || distance < closestDistance))
			{
				closest = children;
				closestDistance = distance;
			}
		}
		return closest != null ? closest.Find("MultiDeckerUI").Find("IDText").GetComponent<UnityEngine.UI.Text>().text : null;
	}
	// Update is called once per frame
	void Update () {
		if (!activated || moduleSolved) return;
		var timeRemaininingMod60 = Mathf.FloorToInt(Mathf.Abs(bombInfo.GetTime()) % 60);
		if (lastTimeMod60 != timeRemaininingMod60)
        {
			lastTimeMod60 = timeRemaininingMod60;
			HandleUpdateTick();
        }
	}
#pragma warning disable 414
	private string TwitchHelpMessage = "\"!{0} check\" [Checks what U module is paired to.] | \"!{0} hover/hl/highlight\" [Highlights the letter.] | \"!{0} cb/colorblind/colourblind\" [Toggles colorblind mode.] | \"!{0} press/submit ## # ##\" [Presses the letter when the seconds timer displays any of the values.]";
#pragma warning restore 414
	IEnumerator ProcessTwitchCommand(string command)
    {
		var allowedHighlightCmds = new[] { "hover", "highlight", "hl" };
		var allowedColorblindCmds = new[] { "cb", "colorblind", "colourblind" };
		if (!activated)
        {
			yield return "sendtochaterror I cannot interact right now! Wait a bit until the module is ready!";
			yield break;
        }
		if (command.EqualsIgnoreCase("check"))
        {
			yield return null;
			pairSelectable.OnInteract();
			if (linkedUModule != null)
				yield return string.Format("sendtochat This module is linked with U #{0}", linkedUModule.GetModuleCode());
			else
				yield return string.Format("sendtochat This module is not linked.");
		}
		else if (allowedHighlightCmds.Any(command.EqualsIgnoreCase))
        {
			yield return null;
			letterSelectable.OnHighlight();
			yield return new WaitForSeconds(2f);
			letterSelectable.OnHighlightEnded();
		}
		else if (allowedColorblindCmds.Any(command.EqualsIgnoreCase))
        {
			yield return null;
			requireColorblind ^= true;
			HandleUpdateTick();
		}
		else
        {
			var regexSubmit = Regex.Match(command, @"^(submit|press)(\s\d{1,2})+");
			if (regexSubmit.Success)
            {
				var possibleValues = regexSubmit.Value.Split().Skip(1).Select(a => int.Parse(a));
				yield return null;
				while (!possibleValues.Any(a => a == Mathf.FloorToInt(Mathf.Abs(bombInfo.GetTime()) % 60)))
					yield return "trycancel I have stopped trying to submit at the values provided.";
				letterSelectable.OnInteract();
            }
        }
		yield break;
    }
	IEnumerator TwitchHandleForcedSolve()
    {
		while (Mathf.FloorToInt(bombInfo.GetTime()) % 15 != targetValue % 15)
			yield return true;
		letterSelectable.OnInteract();
    }
}
