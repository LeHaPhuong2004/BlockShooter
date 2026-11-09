using UnityEngine;
using System.Collections;

public class ScrollingUVRay : MonoBehaviour 
{
	//public int materialIndex = 0;
	public Vector2 uvAnimationRate = new Vector2( 1.0f, 0.0f );
	public string textureName = "_MainTex";
	
	Vector2 uvOffset = Vector2.zero; //new Vector2 (0f, 0.5f); //
	
	void LateUpdate() 
	{
		uvOffset += ( uvAnimationRate * Time.deltaTime );
		if (uvOffset.y <= -10f) {
			uvOffset = Vector2.zero; //new Vector2 (0f, 0.5f);
		}
		if( GetComponent<Renderer>().enabled )
		{
			GetComponent<Renderer>().sharedMaterial.SetTextureOffset( textureName, uvOffset );
		}
	}
}