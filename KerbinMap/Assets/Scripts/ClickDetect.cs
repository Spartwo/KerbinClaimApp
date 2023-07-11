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
    [SerializeField] private GameObject tileScanTarget;
    public TileData selectedTile;
    public string selectedName;

    private MapGen mapSource;
    private Texture2D tileMap;


    // Start is called before the first frame update
    void Start()
    {
        mapSource = GetComponent<MapGen>();
        tileMap = mapSource.tileMap;
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
                int x = Mathf.RoundToInt(textureCoord.x * tileMap.width * 2);
                int y = Mathf.RoundToInt(textureCoord.y * tileMap.height);

                selectedTile = SelectTile(x, y);
                // Get the localised name from the MapGen script
                selectedName = mapSource.RetrieveName(false, selectedTile.ProvinceParent);
            }
        }
    }

    TileData SelectTile(int x, int y)
    {
        // Get the hexcode of the continent pixel
        Color textureColor = mapSource.continentMap.GetPixel(x, y);
        string continentColour = ColorUtility.ToHtmlStringRGB(textureColor);
        // Get the hexcode of the province pixel
        textureColor = mapSource.provinceMap.GetPixel(x, y);
        string provinceColour = ColorUtility.ToHtmlStringRGB(textureColor);
        // Get the hexcode of the tile pixel
        textureColor = tileMap.GetPixel(x, y);
        string tileColour = ColorUtility.ToHtmlStringRGB(textureColor);

        return FindTile(continentColour, provinceColour, tileColour);
    }

    TileData FindTile(string continentColour, string provinceColour, string tileColour)
    {
        //method searches an appropriate subdatabase for the right tile data, reduces search time
        // Find right continent
        foreach (ContinentData c in mapSource.continents)
        {
            if (c.HexCode != continentColour) continue;
            // Find right province inside continent
            foreach (ProvinceData p in c.Provinces)
            {
                if (p.HexCode != provinceColour) continue;
                // Find the tile
                foreach (TileData t in p.Tiles)
                {
                    if (t.HexCode == tileColour)
                    {
                        Debug.Log("Tile " + tileColour + " Found"); 
                        return t;
                    }
                }
            }
        }

        Debug.Log("Tile " + tileColour + " not Found");
        return null;
    }

}
