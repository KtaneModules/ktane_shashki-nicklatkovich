using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShashkiPuzzle {
	public const int MOVES_TO_DRAW = 6;
	public const int PLAYERS_COUNT = 2;

	public static int Sign(int a) { return a == 0 ? 0 : (a < 0 ? -1 : 1); }

	public class Cell {
		public bool passable = true;
		public bool king = false;
		public int player = 0;
	}

	public readonly int[] YDirections = new[] { 0, 1, -1 };

	public enum State {
		TURN,
		STREAK,
		ENDED,
	}

	public readonly Vector2Int BoardSize;

	private int _winner = -1;
	public int winner { get { return _winner; } private set { _winner = value; } }

	private int _player = 1;
	public int player { get { return _player; } }
	private State _state = State.TURN;
	public State state { get { return _state; } private set { _state = value; } }

	public Queue<Move> moves = new Queue<Move>();
	public List<string> notation = new List<string>();

	private int movesToDraw = MOVES_TO_DRAW;
	private Vector2Int _streakPos;
	private Cell[][] _board;

	public ShashkiPuzzle(Vector2Int boardSize, int homeSize) {
		if (homeSize >= boardSize.y / 2) throw new UnityException("Invalid home size");
		BoardSize = boardSize;
		_board = new Cell[BoardSize.x][];
		for (int x = 0; x < BoardSize.x; x++) {
			_board[x] = new Cell[BoardSize.y];
			for (int y = 0; y < BoardSize.y; y++) {
				Cell cell = new Cell();
				_board[x][y] = cell;
				if (x % 2 != y % 2) cell.passable = false;
				else if (y < homeSize) cell.player = 1;
				else if (y >= BoardSize.y - homeSize) cell.player = 2;
			}
		}
	}

	public Cell GetCell(Vector2Int pos) {
		return _board[pos.x][pos.y];
	}

	public class Move {
		public readonly Vector2Int from;
		public readonly Vector2Int to;
		public readonly bool enemiesOnTheWay;
		public Move(Vector2Int from, Vector2Int to, bool enemiesOnTheWay) {
			this.from = from;
			this.to = to;
			this.enemiesOnTheWay = enemiesOnTheWay;
		}
		public override string ToString() {
			return PosToCoord(from) + (enemiesOnTheWay ? ":" : "-") + PosToCoord(to);
		}
	}

	public static string PosToCoord(Vector2Int pos) {
		return (char)(pos.x + 'a') + pos.y.ToString();
	}

	public bool TryMove(Vector2Int from, Vector2Int to) {
		int prevPlayer = player;
		Move move = GetPossibleMoves().FirstOrDefault(m => m.from == from && m.to == to);
		if (move == null) return false;
		// Debug.Log(player);
		// Debug.Log(move.from);
		// Debug.Log(move.to);
		_board[to.x][to.y] = _board[from.x][from.y];
		_board[from.x][from.y] = new Cell();
		Vector2Int dir = new Vector2Int(Sign(to.x - from.x), Sign(to.y - from.y));
		Vector2Int pos = from + dir;
		while (pos != to) {
			_board[pos.x][pos.y] = new Cell();
			pos += dir;
		}
		bool canLeadToDraw = _board[to.x][to.y].king && _state == State.TURN;
		if (to.y == 0 && player == 2) _board[to.x][to.y].king = true;
		if (to.y == BoardSize.y - 1 && player == 1) _board[to.x][to.y].king = true;
		if (_state == State.STREAK) {
			string prevNotation = notation.Last();
			notation.RemoveAt(notation.Count - 1);
			notation.Add(prevNotation + ":" + PosToCoord(to));
		} else notation.Add(PosToCoord(from) + (move.enemiesOnTheWay ? ":" : "-") + PosToCoord(to));
		_state = State.TURN;
		if (move.enemiesOnTheWay && GetPossibleMoves().Any(m => m.enemiesOnTheWay && m.from == to)) {
			_state = State.STREAK;
			_streakPos = to;
		} else {
			_player += 1;
			if (player > PLAYERS_COUNT) _player = 1;
		}
		moves.Enqueue(move);
		if (canLeadToDraw && !move.enemiesOnTheWay) {
			movesToDraw -= 1;
			if (movesToDraw <= 0) {
				winner = 0;
				_state = State.ENDED;
			}
		} else {
			movesToDraw = MOVES_TO_DRAW;
			if (_board.All(row => row.All(cell => cell.player != player)) || GetPossibleMoves().Count == 0) {
				winner = prevPlayer;
				_state = State.ENDED;
			}
		}
		return true;
	}

	public void MakeMove(Vector2Int from, Vector2Int to) {
		bool success = TryMove(from, to);
		if (!success) throw new UnityException("Invalid move provided");
	}

	public List<Move> GetPossibleMoves(bool jumpIsRequired = true) {
		List<Move> result = new List<Move>();
		for (int x = 0; x < BoardSize.x; x++) {
			for (int y = 0; y < BoardSize.y; y++) {
				Cell cell = _board[x][y];
				if (cell.player != player) continue;
				Vector2Int pos = new Vector2Int(x, y);
				int maxStepsCount = cell.king ? int.MaxValue : 1;
				foreach (int yDir in new[] { -1, 1 }) {
					foreach (int xDir in new[] { -1, 1 }) {
						Vector2Int dir = new Vector2Int(xDir, yDir);
						int enemies = 0;
						int steps = 0;
						Vector2Int nextPos = pos;
						while (true) {
							nextPos += dir;
							if (!OnTheBoard(nextPos)) break;
							if (_board[nextPos.x][nextPos.y].player == 0) {
								steps += 1;
								if (cell.king || enemies > 0 || yDir == (cell.player == 1 ? 1 : -1)) result.Add(new Move(pos, nextPos, enemies > 0));
							} else if (_board[nextPos.x][nextPos.y].player == player) break;
							else enemies += 1;
							if (enemies > 1) break;
							if (steps >= maxStepsCount) break;
						}
					}
				}
			}
		}
		if (state == State.STREAK) {
			result = result.Where(m => m.from == _streakPos && m.enemiesOnTheWay).ToList();
			if (result.Count == 0) throw new UnityException("Unable to get possible streaks");
			return result;
		}
		if (jumpIsRequired && result.Any(m => m.enemiesOnTheWay)) return result.Where(m => m.enemiesOnTheWay).ToList();
		// if (result.Count == 0) throw new UnityException("Unable to get possible moves");
		return result;
	}

	public bool OnTheBoard(Vector2Int pos) {
		return pos.x >= 0 && pos.x < BoardSize.x && pos.y >= 0 && pos.y < BoardSize.y;
	}

	public bool PlayerHasPieces(int player) {
		return _board.SelectMany(row => row).Any(c => c.player == player);
	}

	public void TechnicalDefeat(int winner) {
		this.winner = winner;
		state = State.ENDED;
	}
}
