using UnityEngine;

public class PieceComponent : MonoBehaviour {
	private ShashkiPuzzle.Cell _status = ShashkiPuzzle.Cell.EMPTY;
	public ShashkiPuzzle.Cell status {
		get { return _status; }
		set {
			if (value == ShashkiPuzzle.Cell.EMPTY) throw new UnityException("Invalid piece status");
			_status = value;
			UpdateMaterial();
		}
	}

	public Material WhiteManMaterial;
	public Material BlackManMaterial;
	public Renderer Renderer;

	private void UpdateMaterial() {
		if (status == ShashkiPuzzle.Cell.PLAYER_MAN) Renderer.material = WhiteManMaterial;
		else if (status == ShashkiPuzzle.Cell.AI_MAN) Renderer.material = BlackManMaterial;
		else throw new UnityException("Unknown piece material");
	}
}
