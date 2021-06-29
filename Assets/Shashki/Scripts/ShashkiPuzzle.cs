using System;
using System.Collections.Generic;
using UnityEngine;

public class ShashkiPuzzle {
	public enum Cell {
		WHITE,
		EMPTY,
		PLAYER_MAN,
		PLAYER_KING,
		AI_MAN,
		AI_KING,
	}

	public enum State {
		PLAYER_TURN,
		PLAYER_STREAK,
		AI_TURN,
		AI_STREAK,
		WIN,
		DEFEAT,
		DRAW,
	}

	public readonly Vector2Int BoardSize;

	private State _state;
	public State state { get { return _state; } }

	private Vector2Int _streakPos;
	private Cell[][] _board;

	public ShashkiPuzzle(Vector2Int boardSize, int homeSize) {
		if (homeSize >= boardSize.y / 2) throw new UnityException("Invalid home size");
		BoardSize = boardSize;
		_board = new Cell[BoardSize.x][];
		for (int x = 0; x < BoardSize.x; x++) {
			_board[x] = new Cell[BoardSize.y];
			for (int y = 0; y < BoardSize.y; y++) {
				Func<Cell> getCell = () => {
					if (x % 2 == y % 2) return Cell.WHITE;
					return Cell.EMPTY;
				};
				if (x % 2 == y % 2) _board[x][y] = Cell.WHITE;
				else if (y < homeSize) _board[x][y] = Cell.PLAYER_MAN;
				else if (y >= BoardSize.y - homeSize) _board[x][y] = Cell.AI_MAN;
				else _board[x][y] = Cell.EMPTY;
			}
		}
	}

	public Cell GetCell(Vector2Int pos) {
		return _board[pos.x][pos.y];
	}

	public bool TryMove(Vector2Int from, Vector2Int to) {
		int moveLength = Mathf.Abs(from.x - to.x);
		if (moveLength == 0) return false;
		if (moveLength != Mathf.Abs(from.y - to.y)) return false;
		if (_board[from.x][from.y] == Cell.PLAYER_MAN) {
			if (moveLength > 2) return false;
			if (moveLength == 1) {
				if (_board[to.x][to.y] != Cell.EMPTY) return false;
				_board[to.x][to.y] = Cell.PLAYER_MAN;
				_board[from.x][from.y] = Cell.EMPTY;
				_state = State.AI_TURN;
				return true;
			}
			Vector2Int dir = new Vector2Int(Mathf.RoundToInt(Mathf.Sign(to.x - from.x)), Mathf.RoundToInt(Mathf.Sign(to.y - from.y)));
			Vector2Int next = from + dir;
			if (_board[next.x][next.y] != Cell.AI_MAN && _board[next.x][next.y] != Cell.AI_KING) return false;
			_board[to.x][to.y] = Cell.PLAYER_MAN;
			_board[from.x][from.y] = Cell.EMPTY;
			_board[next.x][next.y] = Cell.EMPTY;
			// TODO: check STREAK
			_state = State.AI_TURN;
			return true;
		}
		if (_board[from.x][from.y] == Cell.AI_MAN) {
			if (moveLength > 2) return false;
			if (moveLength == 1) {
				if (_board[to.x][to.y] != Cell.EMPTY) return false;
				_board[to.x][to.y] = Cell.AI_MAN;
				_board[from.x][from.y] = Cell.EMPTY;
				_state = State.PLAYER_TURN;
				return true;
			}
			// TODO: jump
			return false;
		}
		return false;
	}

	public void Move(Vector2Int from, Vector2Int to) {
		if (!TryMove(from, to)) throw new UnityException("Invalid move");
	}

	public List<KeyValuePair<Vector2Int, Vector2Int>> GetPossibleMoves() {
		List<KeyValuePair<Vector2Int, Vector2Int>> result = new List<KeyValuePair<Vector2Int, Vector2Int>>();
		if (state == State.AI_TURN) {
			for (int x = 0; x < BoardSize.x; x++) {
				for (int y = 0; y < BoardSize.y; y++) {
					Vector2Int pos = new Vector2Int(x, y);
					Cell cell = _board[x][y];
					if (cell == Cell.AI_MAN) {
						if (x > 0 && _board[x - 1][y - 1] == Cell.EMPTY) result.Add(new KeyValuePair<Vector2Int, Vector2Int>(pos, new Vector2Int(x - 1, y - 1)));
						if (x < BoardSize.x - 1 && _board[x + 1][y - 1] == Cell.EMPTY) result.Add(new KeyValuePair<Vector2Int, Vector2Int>(pos, new Vector2Int(x + 1, y - 1)));
					}
				}
			}
		}
		return result;
	}
}
