using System;
using System.IO;
using Unity.Entities.Serialization;
using UnityEngine;

namespace Tiler
{
    public static class Serialization
    {
        public static void SaveMap(ref Map map, string folderPath)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            
            DirectoryInfo di = new DirectoryInfo(folderPath);

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete(); 
            }
            
            foreach (var chunk in map._chunks)
            {
                string fileName = $"{chunk.Key.x}_{chunk.Key.y}.chunk";
                string path = Path.Combine(folderPath, fileName);
                    
                using (StreamBinaryWriter writer = new StreamBinaryWriter(path))
                {
                    BinaryWriterExtensions.WriteArray(writer, chunk.Value.Tiles);
                }
            }
        }
        
        public static void SaveAtmospherics(ref Atmospherics atmospherics, string folderPath)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            
            DirectoryInfo di = new DirectoryInfo(folderPath);

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete(); 
            }
            
            foreach (var chunk in atmospherics._chunks)
            {
                string fileName = $"{chunk.Key.x}_{chunk.Key.y}.chunk";
                string path = Path.Combine(folderPath, fileName);
                    
                using (StreamBinaryWriter writer = new StreamBinaryWriter(path))
                {
                    BinaryWriterExtensions.WriteArray(writer, chunk.Value.Gas[Convert.ToInt32(atmospherics.flag)]);
                }
            }
        }
        
        public static void LoadMap(ref Map map, string folderPath)
        {
            DirectoryInfo di = new DirectoryInfo(folderPath);
            
            foreach (FileInfo file in di.GetFiles())
            {
                string[] elements = Path.GetFileNameWithoutExtension(file.Name).Split('_');
                int x = Int32.Parse(elements[0]);
                int y = Int32.Parse(elements[1]);
                
                using (StreamBinaryReader reader = new StreamBinaryReader(file.FullName))
                {
                    Map.Chunk chunk = new Map.Chunk(map._chunkSize);
                    BinaryReaderExtensions.ReadArray(reader, chunk.Tiles, map._chunkSize * map._chunkSize);
                    map.AddChunk(x, y, chunk);
                }
            }
        }
        
        public static void LoadAtmospherics(ref Atmospherics atmospherics, string folderPath)
        {
            DirectoryInfo di = new DirectoryInfo(folderPath);
            
            foreach (FileInfo file in di.GetFiles())
            {
                string[] elements = Path.GetFileNameWithoutExtension(file.Name).Split('_');
                int x = Int32.Parse(elements[0]);
                int y = Int32.Parse(elements[1]);
                
                using (StreamBinaryReader reader = new StreamBinaryReader(file.FullName))
                {
                    Atmospherics.Chunk chunk = new Atmospherics.Chunk(atmospherics.size);
                    BinaryReaderExtensions.ReadArray(reader, chunk.Gas[Convert.ToInt32(atmospherics.flag)], atmospherics.size * atmospherics.size);
                    chunk.Gas[Convert.ToInt32(atmospherics.flag)].CopyTo(chunk.Gas[Convert.ToInt32(!atmospherics.flag)]);
                    atmospherics.AddChunk(x, y, chunk);
                }
            }
        }
    }
}
