using UnityEngine;

public class PieceComponent : MonoBehaviour {
	private bool _king;
	public bool king { get { return _king; } set { _king = value; UpdateMaterial(); } }

	private int _player;
	public int player { get { return _player; } set { _player = value; UpdateMaterial(); } }

	public GameObject KingLogo;
	public Material[] PlayersMaterials;
	public Renderer Renderer;

	private void Start() {
		if (!king) KingLogo.SetActive(false);
	}

	private void UpdateMaterial() {
		Renderer.material = PlayersMaterials[player];
		KingLogo.SetActive(king);
	}
}
