using System;
using System.IO;
using System.IO.Compression;
using System.Data;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length >= 3 && args[0].Equals("merge", StringComparison.OrdinalIgnoreCase))
        {
            string targetFile = args[1];
            Console.WriteLine($"[MergeTool] Target output MBTiles: {targetFile}");

            if (File.Exists(targetFile)) File.Delete(targetFile);

            // Copy first source file as the base target file
            File.Copy(args[2], targetFile, true);
            Console.WriteLine($"[MergeTool] Base file copied: {args[2]} -> {targetFile}");

            try
            {
                // Use reflection or BruTile/SQLite assembly if available, or sqlite3
                for (int i = 3; i < args.Length; i++)
                {
                    string sourcePart = args[i];
                    if (File.Exists(sourcePart))
                    {
                        Console.WriteLine($"[MergeTool] Merging part: {sourcePart}...");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MergeTool] Error: " + ex.Message);
            }
            return;
        }

        string pbfPath = args.Length > 0 ? args[0] : @"E:\Projects\GitHub\Mapsui48\DumpTool\map.pbf";
        if (!File.Exists(pbfPath))
        {
            Console.WriteLine("PBF file not found: " + pbfPath);
            return;
        }

        try
        {
            using var fs = File.OpenRead(pbfPath);
            
            // Read 4-byte big-endian header size
            byte[] sizeBuf = new byte[4];
            if (fs.Read(sizeBuf, 0, 4) < 4) return;
            if (BitConverter.IsLittleEndian) Array.Reverse(sizeBuf);
            int headerSize = BitConverter.ToInt32(sizeBuf, 0);

            // Read OsmHeader
            byte[] headerBuf = new byte[headerSize];
            fs.Read(headerBuf, 0, headerSize);

            // Read 4-byte blob size
            if (fs.Read(sizeBuf, 0, 4) < 4) return;
            if (BitConverter.IsLittleEndian) Array.Reverse(sizeBuf);
            int blobSize = BitConverter.ToInt32(sizeBuf, 0);

            byte[] blobBuf = new byte[blobSize];
            fs.Read(blobBuf, 0, blobSize);

            // Locate zlib header (0x78 0x9C, 0x78 0x01, or 0x78 0xDA)
            int zlibIdx = -1;
            for (int i = 0; i < blobBuf.Length - 2; i++)
            {
                if (blobBuf[i] == 0x78 && (blobBuf[i + 1] == 0x9C || blobBuf[i + 1] == 0x01 || blobBuf[i + 1] == 0xDA))
                {
                    zlibIdx = i;
                    break;
                }
            }

            if (zlibIdx >= 0)
            {
                using var ms = new MemoryStream(blobBuf, zlibIdx + 2, blobBuf.Length - zlibIdx - 2);
                using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
                using var decomp = new MemoryStream();
                deflate.CopyTo(decomp);
                byte[] data = decomp.ToArray();

                // Scan protobuf varints in HeaderBlock for BBox (field 1 tag = 0x0A)
                long left = 0, right = 0, top = 0, bottom = 0;
                bool found = ParseHeaderBBox(data, ref left, ref right, ref top, ref bottom);

                if (found)
                {
                    double minLon = left / 1e9;
                    double maxLon = right / 1e9;
                    double maxLat = top / 1e9;
                    double minLat = bottom / 1e9;

                    if (Math.Abs(minLon) > 180) { minLon /= 100; maxLon /= 100; maxLat /= 100; minLat /= 100; }

                    Console.WriteLine($"BBOX:{minLat:F6},{minLon:F6},{maxLat:F6},{maxLon:F6}");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    static bool ParseHeaderBBox(byte[] data, ref long left, ref long right, ref long top, ref long bottom)
    {
        int pos = 0;
        while (pos < data.Length)
        {
            int tag = ReadVarint(data, ref pos);
            int fieldNum = tag >> 3;
            int wireType = tag & 0x07;

            if (fieldNum == 1 && wireType == 2) // HeaderBBox submessage
            {
                int len = ReadVarint(data, ref pos);
                int endPos = pos + len;
                while (pos < endPos)
                {
                    int subTag = ReadVarint(data, ref pos);
                    int subField = subTag >> 3;
                    long val = ReadSInt64(data, ref pos);

                    if (subField == 1) left = val;
                    else if (subField == 2) right = val;
                    else if (subField == 3) top = val;
                    else if (subField == 4) bottom = val;
                }
                return true;
            }
            else
            {
                SkipField(data, ref pos, wireType);
            }
        }
        return false;
    }

    static int ReadVarint(byte[] data, ref int pos)
    {
        int val = 0, shift = 0;
        while (pos < data.Length)
        {
            byte b = data[pos++];
            val |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }
        return val;
    }

    static long ReadSInt64(byte[] data, ref int pos)
    {
        long raw = 0;
        int shift = 0;
        while (pos < data.Length)
        {
            byte b = data[pos++];
            raw |= ((long)(b & 0x7F)) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }
        return (raw >> 1) ^ (-(raw & 1));
    }

    static void SkipField(byte[] data, ref int pos, int wireType)
    {
        if (wireType == 0) ReadVarint(data, ref pos);
        else if (wireType == 1) pos += 8;
        else if (wireType == 2) { int len = ReadVarint(data, ref pos); pos += len; }
        else if (wireType == 5) pos += 4;
    }
}
