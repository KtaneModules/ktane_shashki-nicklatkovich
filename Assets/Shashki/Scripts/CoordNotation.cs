using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoordNotation : MonoBehaviour {
	private string _text = "";
	public string text { get { return _text; } set { _text = value; UpdateText(); } }

	private Color _color = new Color32(0x55, 0x55, 0x55, 0xff);
	public Color color { get { return _color; } set { _color = value; UpdateText(); } }

	public TextMesh Text;

	private void Start() {
		UpdateText();
	}

	public void UpdateText() {
		Text.text = text;
		Text.color = color;
	}
}
