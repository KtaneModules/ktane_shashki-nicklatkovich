using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class ShashkiModule : MonoBehaviour {
	public const int GAMES_TO_RESET_COUNT = 15;
	public const int WIN_STREAK_REQUIRED = 3;
	public const int MAX_DRAWS_COUNT = 6;
	public const int HOME_SIZE = 3;
	public const float LEDS_INTERVAL = .009f;
	public const float RESTART_TIMER = 1f;
	public const float MOVE_ANIMATION_TIME = .3f;
	public const float NOTATION_TEXT_Y_OFFSET = .0081f;
	public const float PIECE_Y_OFFSET = .005f + .002f / 2;
	public const float PIECE_JUMP_HEIGHT = .002f;
	public const string WIN_SOUND = "Game_win";
	public readonly Vector2Int CHECKERBOARD_SIZE = new Vector2Int(8, 8);
	public readonly Vector2 CHECKERBOARD_CELLS_OFFSET = new Vector2(.018f, .018f);

	private static int moduleIdCounter = 1;

	public readonly string TwitchHelpMessage = "\"!{0} a3-b4\" - move piece | \"!{0} a3:c5\" - make a jump | \"!{0} a3:c5:e3\" - make a multiple-jumps";

	public string[] MovingSounds;
	public string[] JumpingSounds;
	public GameObject BoardContainer;
	public GameObject LEDSContainer;
	public GameObject StatusLight;
	public Material WhiteCellMaterial;
	public Material BlackCellMaterial;
	public KMSelectable Selectable;
	public KMBombModule Module;
	public KMAudio Audio;
	public CellComponent CellPrefab;
	public PieceComponent PiecePrefab;
	public LEDComponent LEDPrefab;
	public CoordNotation CoordNotationPrefab;

	public bool TwitchPlaysActive;

	private int moduleId;
	private int totalPassedGamesCount = 0;
	private int passedGamesCount = 0;
	private int winStreak = 0;
	private int draws = 0;
	private bool solved = false;
	private bool activated;
	private Vector2Int? selectedCell;
	private CellComponent[][] cells;
	private ShashkiPuzzle puzzle;
	private PieceComponent[][] pieces;
	private float currentMoveProgress = 0f;
	private List<LEDComponent> leds = new List<LEDComponent>();

	public Vector3 cellToPos(Vector2Int pos) {
		Vector2 floatPos = new Vector2(pos.x + .5f - CHECKERBOARD_SIZE.x / 2f, pos.y + .5f - CHECKERBOARD_SIZE.y / 2f);
		return new Vector3(floatPos.x * CHECKERBOARD_CELLS_OFFSET.x, 0, floatPos.y * CHECKERBOARD_CELLS_OFFSET.y);
	}

	private void Start() {
		moduleId = moduleIdCounter++;
		cells = new CellComponent[CHECKERBOARD_SIZE.x][];
		for (int x = 0; x < CHECKERBOARD_SIZE.x; x++) {
			cells[x] = new CellComponent[CHECKERBOARD_SIZE.y];
			for (int y = 0; y < CHECKERBOARD_SIZE.y; y++) {
				Vector2Int pos = new Vector2Int(x, y);
				CellComponent cell = Instantiate(CellPrefab);
				cells[x][y] = cell;
				bool black = x % 2 != y % 2;
				cell.Renderer.material = black ? BlackCellMaterial : WhiteCellMaterial;
				cell.transform.parent = BoardContainer.transform;
				cell.transform.localPosition = cellToPos(pos);
				cell.transform.localScale = Vector3.one;
				cell.transform.rotation = Quaternion.identity;
				cell.Selectable.Parent = Selectable;
				cell.Selectable.OnInteract += () => { CellPressed(pos); return false; };
			}
		}
		Selectable.Children = cells.SelectMany(row => row.Select(cell => cell.Selectable)).ToArray();
		Selectable.UpdateChildren();
		Module.OnActivate += Activate;
		FixStatusLight();
	}

	private void FixStatusLight() {
		int childCount = StatusLight.transform.childCount;
		for (int i = 0; i < childCount; i++) {
			GameObject child = StatusLight.transform.GetChild(i).gameObject;
			if (child.name.Contains("Twitch")) continue;
			child.SetActive(false);
		}
	}

	private void DebugGame(string stringAfterLastMove = null) {
		string[] notation = puzzle.notation.ToArray();
		for (int i = 0; i < notation.Length; i += 2) {
			Debug.LogFormat("[Shashki #{0}] Move #{1}: {2}", moduleId, i / 2 + 1, notation[i] + (i < notation.Length - 1 ? " " + notation[i + 1] : ""));
		}
		if (stringAfterLastMove != null) Debug.LogFormat("[Shashki #{0}] {1}", moduleId, stringAfterLastMove);
		string winner = puzzle.winner == 0 ? "DRAW" : (puzzle.winner == 1 ? "Player" : "Module");
		Debug.LogFormat("[Shashki #{0}] Game #{1} ended. Winner: {2}", moduleId, totalPassedGamesCount + 1, winner);
	}

	private IEnumerator RestartTimer(int count = 1) {
		GameEnded(puzzle.winner, count);
		yield return new WaitForSeconds(RESTART_TIMER);
		Restart();
	}

	private void Restart() {
		if (passedGamesCount >= GAMES_TO_RESET_COUNT) {
			Debug.LogFormat("[Shashki #{0}] {1} games played. Reset module", moduleId, GAMES_TO_RESET_COUNT);
			passedGamesCount = 0;
			foreach (LEDComponent led in leds) Destroy(led.gameObject);
			leds = new List<LEDComponent>();
			winStreak = 0;
			draws = 0;
		}
		Debug.LogFormat("[Shashki #{0}] Game #{1} started", moduleId, totalPassedGamesCount + 1);
		if (pieces != null) {
			foreach (PieceComponent piece in pieces.SelectMany(row => row).Where(p => p != null)) Destroy(piece.gameObject);
		}
		puzzle = new ShashkiPuzzle(CHECKERBOARD_SIZE, HOME_SIZE);
		pieces = new PieceComponent[CHECKERBOARD_SIZE.x][];
		for (int x = 0; x < CHECKERBOARD_SIZE.x; x++) {
			pieces[x] = new PieceComponent[CHECKERBOARD_SIZE.y];
			for (int y = 0; y < CHECKERBOARD_SIZE.y; y++) {
				Vector2Int pos = new Vector2Int(x, y);
				ShashkiPuzzle.Cell cellStatus = puzzle.GetCell(pos);
				if (cellStatus.player == 0) continue;
				PieceComponent piece = Instantiate(PiecePrefab);
				piece.transform.parent = BoardContainer.transform;
				piece.transform.localPosition = cellToPos(pos) + new Vector3(0, PIECE_Y_OFFSET, 0);
				piece.transform.localScale = Vector3.one;
				piece.transform.localRotation = Quaternion.identity;
				piece.player = cellStatus.player;
				pieces[x][y] = piece;
			}
		}
	}

	private void Update() {
		if (!activated) return;
		if (puzzle.moves.Count == 0) return;
		ShashkiPuzzle.Move move = puzzle.moves.First();
		if (currentMoveProgress <= 0) Audio.PlaySoundAtTransform(move.enemiesOnTheWay ? JumpingSounds.PickRandom() : MovingSounds.PickRandom(), transform);
		currentMoveProgress += Time.deltaTime;
		float animationTime = Mathf.Min(1f, currentMoveProgress / MOVE_ANIMATION_TIME);
		Vector3 diff = cellToPos(move.to) - cellToPos(move.from);
		Vector3 newPos = cellToPos(move.from) + animationTime * diff;
		PieceComponent piece = pieces[move.from.x][move.from.y];
		piece.transform.localPosition = new Vector3(newPos.x, PIECE_Y_OFFSET + PIECE_JUMP_HEIGHT * Mathf.Sin(animationTime * Mathf.PI), newPos.z);
		Vector2Int dir = new Vector2Int(ShashkiPuzzle.Sign(move.to.x - move.from.x), ShashkiPuzzle.Sign(move.to.y - move.from.y));
		Vector2Int pos = move.from + dir;
		while (pos != move.to) {
			if (pieces[pos.x][pos.y] != null && (float)(pos.x - move.from.x) / (move.to.x - move.from.x) <= animationTime) {
				Destroy(pieces[pos.x][pos.y].gameObject);
				pieces[pos.x][pos.y] = null;
			}
			pos += dir;
		}
		if (animationTime >= 1f) {
			pieces[move.to.x][move.to.y] = piece;
			pieces[move.from.x][move.from.y] = null;
			if (puzzle.GetCell(move.to).king && !piece.king) piece.king = true;
			puzzle.moves.Dequeue();
			currentMoveProgress = 0f;
		}
	}

	private void Activate() {
		activated = true;
		if (TwitchPlaysActive) {
			for (int x = 1; x < 8; x++) {
				CoordNotation coord = Instantiate(CoordNotationPrefab);
				coord.transform.parent = BoardContainer.transform;
				coord.transform.localPosition = cellToPos(new Vector2Int(x, 7)) + Vector3.up * NOTATION_TEXT_Y_OFFSET;
				coord.transform.localScale = Vector3.one;
				coord.transform.localRotation = Quaternion.identity;
				coord.text = ((char)(x + 'A')).ToString();
				coord.color = x % 2 == 1 ? new Color32(0x55, 0x55, 0x55, 0xff) : new Color32(0x28, 0x2d, 0x36, 0xff);
			}
			for (int y = 0; y < 7; y++) {
				CoordNotation coord = Instantiate(CoordNotationPrefab);
				coord.transform.parent = BoardContainer.transform;
				coord.transform.localPosition = cellToPos(new Vector2Int(0, y)) + Vector3.up * NOTATION_TEXT_Y_OFFSET;
				coord.transform.localScale = Vector3.one;
				coord.transform.localRotation = Quaternion.identity;
				coord.text = (y + 1).ToString();
				coord.color = y % 2 == 0 ? new Color32(0x55, 0x55, 0x55, 0xff) : new Color32(0x28, 0x2d, 0x36, 0xff);
			}
			CoordNotation a1coord = Instantiate(CoordNotationPrefab);
			a1coord.transform.parent = BoardContainer.transform;
			a1coord.transform.localPosition = cellToPos(new Vector2Int(0, 7)) + Vector3.up * NOTATION_TEXT_Y_OFFSET;
			a1coord.transform.localScale = Vector3.one;
			a1coord.transform.localRotation = Quaternion.identity;
			a1coord.text = "A8";
			a1coord.color = new Color32(0x28, 0x2d, 0x36, 0xff);
		}
		Restart();
	}

	private void Solve() {
		solved = true;
		GameEnded(puzzle.winner);
		StartCoroutine(SolveAnimation());
	}

	private IEnumerator SolveAnimation() {
		foreach (LEDComponent led in leds) Destroy(led.gameObject);
		leds = new List<LEDComponent>();
		for (int i = 0; i < GAMES_TO_RESET_COUNT; i++) {
			CreateLED(1, i);
			yield return new WaitForSeconds(.1f);
		}
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
		Module.HandlePass();
	}

	private void CellPressed(Vector2Int pos) {
		if (!activated) return;
		if (puzzle.state == ShashkiPuzzle.State.ENDED) return;
		if (puzzle.player != 1) return;
		if (puzzle.GetCell(pos).player == 0) {
			if (selectedCell == null) return;
			bool success = puzzle.TryMove(selectedCell.Value, pos);
			if (!success) {
				if (puzzle.GetPossibleMoves(false).Any(m => m.from == selectedCell && m.to == pos)) {
					ShashkiPuzzle.Move possibleMove = puzzle.GetPossibleMoves().First();
					string tryingMove = ShashkiPuzzle.PosToCoord(selectedCell.Value) + "-" + ShashkiPuzzle.PosToCoord(pos);
					DebugGame(string.Format("Trying to make move {0} while jump {1} possible", tryingMove, possibleMove.ToString()));
					cells[selectedCell.Value.x][selectedCell.Value.y].selected = false;
					selectedCell = null;
					winStreak = 0;
					Module.HandleStrike();
					puzzle.TechnicalDefeat(2);
					StartCoroutine(RestartTimer());
				}
				return;
			}
			cells[selectedCell.Value.x][selectedCell.Value.y].selected = false;
			if (puzzle.state == ShashkiPuzzle.State.STREAK) {
				selectedCell = pos;
				cells[selectedCell.Value.x][selectedCell.Value.y].selected = true;
			} else {
				selectedCell = null;
				while (puzzle.state != ShashkiPuzzle.State.ENDED && puzzle.player != 1) MakeAITurn();
			}
			if (puzzle.winner == 1) {
				DebugGame();
				int winsCount = 1;
				if (puzzle.PlayerHasPieces(2)) {
					Debug.LogFormat("[Shashki #{0}] Module has pieces. Two wins being credited instead of one!", moduleId);
					winsCount = 2;
				}
				winStreak += winsCount;
				if (winStreak >= WIN_STREAK_REQUIRED) {
					Debug.LogFormat("[Shashki #{0}] {1} wins in a row. Module solved!", moduleId, winStreak);
					Solve();
				} else {
					Audio.PlaySoundAtTransform(WIN_SOUND, transform);
					StartCoroutine(RestartTimer(winsCount));
				}
			} else if (puzzle.winner == 0) {
				winStreak = 0;
				draws += 1;
				DebugGame();
				if (draws >= MAX_DRAWS_COUNT) {
					Debug.LogFormat("[Shashki #{0}] {1} draws. Module solved!", moduleId, draws);
					Solve();
				} else {
					Audio.PlaySoundAtTransform(WIN_SOUND, transform);
					StartCoroutine(RestartTimer());
				}
			} else if (puzzle.winner > 1) {
				Module.HandleStrike();
				winStreak = 0;
				DebugGame();
				if (puzzle.PlayerHasPieces(1)) {
					Debug.LogFormat("[Shashki #{0}] Player has pieces. Two defeats being credited instead of one!", moduleId);
					Module.HandleStrike();
				}
				StartCoroutine(RestartTimer());
			}
			return;
		}
		if (puzzle.GetCell(pos).player == 1) {
			if (puzzle.state == ShashkiPuzzle.State.STREAK) return;
			if (selectedCell != null) cells[selectedCell.Value.x][selectedCell.Value.y].selected = false;
			selectedCell = pos;
			cells[pos.x][pos.y].selected = true;
		}
	}

	private void GameEnded(int winner, int count = 1) {
		totalPassedGamesCount += 1;
		for (int i = 0; i < count; i++) {
			CreateLED(winner, passedGamesCount);
			passedGamesCount += 1;
			if (passedGamesCount >= GAMES_TO_RESET_COUNT) break;
		}
	}

	private void CreateLED(int winner, int pos) {
		LEDComponent led = Instantiate(LEDPrefab);
		led.transform.parent = LEDSContainer.transform;
		led.transform.localPosition = Vector3.right * pos * LEDS_INTERVAL;
		led.transform.localScale = Vector3.one;
		led.transform.localRotation = Quaternion.identity;
		led.winner = winner;
		leds.Add(led);
	}

	public IEnumerator ProcessTwitchCommand(string command) {
		command = command.Trim().ToLower();
		ShashkiPuzzle puzzle = this.puzzle.Copy();
		if (command == "moves") {
			yield return null;
			yield return "sendtochat {0}, !{1} possible moves: " + puzzle.GetPossibleMoves(false).Select(m => m.ToString()).Join("; ");
			yield break;
		}
		if (Regex.IsMatch(command, "^[a-h][1-8]-[a-h][1-8]$")) {
			Vector2Int from = puzzle.CoordToPos(command.Split('-').First());
			Vector2Int to = puzzle.CoordToPos(command.Split('-').Last());
			yield return null;
			if (puzzle.GetPossibleMoves(false).All(m => m.from != from || m.to != to || m.enemiesOnTheWay)) {
				yield return "sendtochat {0}, !{1} unable to move " + command;
				yield break;
			}
			if (selectedCell != from) {
				CellPressed(from);
				yield return new WaitForSeconds(.1f);
			}
			CellPressed(to);
			if (solved) yield return "solve";
			yield break;
		}
		if (Regex.IsMatch(command, "^[a-h][1-8](:[a-h][1-8])+$")) {
			Vector2Int[] split = command.Split(':').Select(c => puzzle.CoordToPos(c)).ToArray();
			yield return null;
			for (int i = 1; i < split.Length; i++) {
				if (!puzzle.TryMove(split[i - 1], split[i]) || !puzzle.moves.Last().enemiesOnTheWay) {
					yield return "sendtochat {0}, !{1} unable to jump " + command;
					yield break;
				}
			}
			if (selectedCell != split[0]) {
				CellPressed(split[0]);
				yield return new WaitForSeconds(.1f);
			}
			for (int i = 1; i < split.Length; i++) {
				if (i > 1) yield return new WaitForSeconds(.1f);
				CellPressed(split[i]);
			}
			if (solved) yield return "solve";
			yield break;
		}
	}

	private void MakeAITurn() {
		ShashkiPuzzle.Move move = puzzle.GetPossibleMoves().PickRandom();
		puzzle.MakeMove(move.from, move.to);
	}
}
