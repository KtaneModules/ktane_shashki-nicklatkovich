using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShashkiModule : MonoBehaviour {
	public const int HOME_SIZE = 3;
	public const float PIECE_Y_OFFSET = .005f + .002f / 2;
	public readonly Vector2Int CHECKERBOARD_SIZE = new Vector2Int(8, 8);
	public readonly Vector2 CHECKERBOARD_CELLS_OFFSET = new Vector2(.018f, .018f);

	public GameObject BoardContainer;
	public Material WhiteCellMaterial;
	public Material BlackCellMaterial;
	public KMSelectable Selectable;
	public KMBombModule Module;
	public CellComponent CellPrefab;
	public PieceComponent PiecePrefab;

	private bool activated;
	private Vector2Int? selectedCell;
	private CellComponent[][] cells;
	private ShashkiPuzzle puzzle;
	private PieceComponent[][] pieces;

	public Vector3 cellToPos(Vector2Int pos) {
		Vector2 floatPos = new Vector2(pos.x + .5f - CHECKERBOARD_SIZE.x / 2f, pos.y + .5f - CHECKERBOARD_SIZE.y / 2f);
		return new Vector3(floatPos.x * CHECKERBOARD_CELLS_OFFSET.x, 0, floatPos.y * CHECKERBOARD_CELLS_OFFSET.y);
	}

	private void Start() {
		puzzle = new ShashkiPuzzle(CHECKERBOARD_SIZE, HOME_SIZE);
		cells = new CellComponent[CHECKERBOARD_SIZE.x][];
		pieces = new PieceComponent[CHECKERBOARD_SIZE.x][];
		for (int x = 0; x < CHECKERBOARD_SIZE.x; x++) {
			cells[x] = new CellComponent[CHECKERBOARD_SIZE.y];
			pieces[x] = new PieceComponent[CHECKERBOARD_SIZE.y];
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
				ShashkiPuzzle.Cell cellStatus = puzzle.GetCell(pos);
				if (cellStatus == ShashkiPuzzle.Cell.WHITE) continue;
				if (cellStatus == ShashkiPuzzle.Cell.EMPTY) continue;
				PieceComponent piece = Instantiate(PiecePrefab);
				piece.transform.parent = BoardContainer.transform;
				piece.transform.localPosition = cellToPos(pos) + new Vector3(0, PIECE_Y_OFFSET, 0);
				piece.transform.localScale = Vector3.one;
				piece.transform.localRotation = Quaternion.identity;
				piece.status = cellStatus;
				pieces[x][y] = piece;
			}
		}
		Selectable.Children = cells.SelectMany(row => row.Select(cell => cell.Selectable)).ToArray();
		Selectable.UpdateChildren();
		Module.OnActivate += Activate;
	}

	private void Activate() {
		activated = true;
	}

	private void CellPressed(Vector2Int pos) {
		if (!activated) return;
		ShashkiPuzzle.Cell cellStatus = puzzle.GetCell(pos);
		if (cellStatus == ShashkiPuzzle.Cell.AI_KING) return;
		if (cellStatus == ShashkiPuzzle.Cell.AI_MAN) return;
		if (cellStatus == ShashkiPuzzle.Cell.WHITE) return;
		if (cellStatus == ShashkiPuzzle.Cell.PLAYER_MAN || cellStatus == ShashkiPuzzle.Cell.PLAYER_KING) {
			if (puzzle.state != ShashkiPuzzle.State.PLAYER_TURN) return;
			if (selectedCell != null) cells[selectedCell.Value.x][selectedCell.Value.y].selected = false;
			if (selectedCell == pos) return;
			cells[pos.x][pos.y].selected = true;
			selectedCell = pos;
			return;
		}
		if (cellStatus == ShashkiPuzzle.Cell.EMPTY) {
			if (selectedCell == null) return;
			bool success = puzzle.TryMove(selectedCell.Value, pos);
			if (!success) return;
			cells[selectedCell.Value.x][selectedCell.Value.y].selected = false;
			pieces[pos.x][pos.y] = pieces[selectedCell.Value.x][selectedCell.Value.y];
			pieces[selectedCell.Value.x][selectedCell.Value.y] = null;
			pieces[pos.x][pos.y].transform.localPosition = cellToPos(pos) + new Vector3(0, PIECE_Y_OFFSET, 0);
			Vector2Int dir = new Vector2Int(Mathf.RoundToInt(Mathf.Sign(pos.x - selectedCell.Value.x)), Mathf.RoundToInt(Mathf.Sign(pos.y - selectedCell.Value.y)));
			while (true) {
				selectedCell = selectedCell + dir;
				if (selectedCell == pos) break;
				if (pieces[selectedCell.Value.x][selectedCell.Value.y] != null) Destroy(pieces[selectedCell.Value.x][selectedCell.Value.y].gameObject);
			}
			selectedCell = null;
			if (puzzle.state == ShashkiPuzzle.State.PLAYER_STREAK) {
				selectedCell = pos;
			} else if (puzzle.state == ShashkiPuzzle.State.AI_TURN) MakeAITurn();
			return;
		}
	}

	private void MakeAITurn() {
		KeyValuePair<Vector2Int, Vector2Int> move = puzzle.GetPossibleMoves().PickRandom();
		puzzle.Move(move.Key, move.Value);
		pieces[move.Value.x][move.Value.y] = pieces[move.Key.x][move.Key.y];
		pieces[move.Key.x][move.Key.y] = null;
		pieces[move.Value.x][move.Value.y].transform.localPosition = cellToPos(move.Value) + new Vector3(0, PIECE_Y_OFFSET, 0);
	}
}
