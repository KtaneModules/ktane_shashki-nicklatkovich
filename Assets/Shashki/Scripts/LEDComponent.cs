using UnityEngine;

public class LEDComponent : MonoBehaviour {

	public Renderer Renderer;

	private int _winner = 0;
	public int winner { get { return _winner; } set { _winner = value; UpdateColor(); } }

	public void UpdateColor() {
		Renderer.material.color = winner == 0 ? Color.yellow : (winner == 1 ? Color.green : Color.red);
	}
}
