using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShashkiModule : MonoBehaviour {
	public const int GAMES_TO_RESET_COUNT = 15;
	public const int WIN_STREAK_REQUIRED = 3;
	public const int MAX_DRAWS_COUNT = 6;
	public const int HOME_SIZE = 3;
	public const float LEDS_INTERVAL = .009f;
	public const float RESTART_TIMER = 1f;
	public const float MOVE_ANIMATION_TIME = .3f;
	public const float PIECE_Y_OFFSET = .005f + .002f / 2;
	public const float PIECE_JUMP_HEIGHT = .002f;
	public const string WIN_SOUND = "Game_win";
	public readonly Vector2Int CHECKERBOARD_SIZE = new Vector2Int(8, 8);
	public readonly Vector2 CHECKERBOARD_CELLS_OFFSET = new Vector2(.018f, .018f);

	public string[] MovingSounds;
	public string[] JumpingSounds;
	public GameObject BoardContainer;
	public GameObject LEDSContainer;
	public Material WhiteCellMaterial;
	public Material BlackCellMaterial;
	public KMSelectable Selectable;
	public KMBombModule Module;
	public KMAudio Audio;
	public CellComponent CellPrefab;
	public PieceComponent PiecePrefab;
	public LEDComponent LEDPrefab;

	private int passedGamesCount = 0;
	private int winStreak = 0;
	private int draws = 0;
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
		cells = new CellComponent[CHECKERBOARD_SIZE.x][];
		for (int x = 0; x < CHECKERBOARD_SIZE.x; x++) {
			cells[x] = new CellComponent[CHECKERBOARD_SIZE.y];
			for (int y = 0; y < CHECKERBOARD_SIZE.y; y++) {
				Vector2Int pos = new Vector2Int(x, y);
				CellComponent cell = Instantiate(CellPrefab);
				cells[x][y] = cell;
				bool black = x % 2 == y % 2;
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
	}

	private IEnumerator RestartTimer(int count = 1) {
		GameEnded(puzzle.winner, count);
		yield return new WaitForSeconds(RESTART_TIMER);
		Restart();
	}

	private void Restart() {
		if (passedGamesCount >= GAMES_TO_RESET_COUNT) {
			passedGamesCount = 0;
			foreach (LEDComponent led in leds) Destroy(led.gameObject);
			leds = new List<LEDComponent>();
			winStreak = 0;
			draws = 0;
		}
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
		Restart();
	}

	private void Solve() {
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
				int winsCount = puzzle.PlayerHasPieces(2) ? 2 : 1;
				winStreak += winsCount;
				if (winStreak >= WIN_STREAK_REQUIRED) Solve();
				else {
					Audio.PlaySoundAtTransform(WIN_SOUND, transform);
					StartCoroutine(RestartTimer(winsCount));
				}
			} else if (puzzle.winner == 0) {
				winStreak = 0;
				draws += 1;
				if (draws >= MAX_DRAWS_COUNT) Solve();
				else {
					Audio.PlaySoundAtTransform(WIN_SOUND, transform);
					StartCoroutine(RestartTimer());
				}
			} else if (puzzle.winner > 1) {
				Module.HandleStrike();
				winStreak = 0;
				if (puzzle.PlayerHasPieces(1)) Module.HandleStrike();
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

	private void MakeAITurn() {
		ShashkiPuzzle.Move move = puzzle.GetPossibleMoves().PickRandom();
		puzzle.MakeMove(move.from, move.to);
	}
}
