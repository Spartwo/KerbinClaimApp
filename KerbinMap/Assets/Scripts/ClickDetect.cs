using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Linq;
using UnityEngine;
using System.IO;

using Color = UnityEngine.Color;
using ColorUtility = UnityEngine.ColorUtility;
using Debug = UnityEngine.Debug;
using Input = UnityEngine.Input;
using File = System.IO.File;

using System.Collections;
using System.Diagnostics;

//this method handles click detection action and the stages involved in generating map data
public class ClickDetect : MonoBehaviour
{
    public TileData selectedTile;

    private MapGen mapSource;
    private GameObject tileScanTarget;
    private Texture2D tileMap;
    private Texture2D continentMap;

    // Start is called before the first frame update
    void Start()
    {
        mapSource = GetComponent<MapGen>();
        tileScanTarget = mapSource.tileScanTarget;
        tileMap = mapSource.tileMap;
        continentMap = mapSource.continentMap;
    }
    void Update()
    {
        // Check for left mouse button click
        if (Input.GetMouseButtonDown(0)) 
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Perform the raycast and check for collision with the tiles
            if (Physics.Raycast(ray, out hit) && hit.collider.gameObject == tileScanTarget)
            {
                //get the position where the ray hit
                Vector2 textureCoord = hit.textureCoord;
                // Convert texture coordinates to pixel coordinates
                int x = Mathf.RoundToInt(textureCoord.x * tileMap.width);
                int y = Mathf.RoundToInt(textureCoord.y * tileMap.height);

                // Get the color of the pixel
                Color textureColor = tileMap.GetPixel(x, y);
                // Get the hexcode of the pixel
                string hexColor = ColorUtility.ToHtmlStringRGB(textureColor);
                Debug.Log("Colour Hit: " + hexColor);

                // Find the tile definition
                // Narrow by continent
                Color continentColor = continentMap.GetPixel(x, y);
                int colorValue = 
                    ((int)(continentColor.r * 255) << 16) |
                    ((int)(continentColor.g * 255) << 8) |
                    (int)(continentColor.b * 255); //this is sus but it as least keeps the door open for smaller divisions

                Debug.Log(colorValue);

                selectedTile = ReturnTile(colorValue, hexColor);

                //add highlight to map
            }
        }
    }

    TileData ReturnTile(int colorValue, String hexColor)
    {
        //method searches an appropriate subdatabase for the right tile data
        //get the right continent cluster file, reduces search time
        List<TileData> reader =  mapSource.getTileList(colorValue);

        for (int i = 0; i < reader.Count; i++)
        {
            Debug.Log(hexColor + " needs " + reader[i].HexCode);
            if (reader[i].HexCode == hexColor)
            {
                return reader[i];
            }
        }

        return new TileData();
    }
    
}
