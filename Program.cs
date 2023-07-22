using System.IO.Compression;
using System.Text;
using SixLabors.ImageSharp.Formats.Png;

namespace tmodpiracy
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                throw new ArgumentException("Missing path to .tmod to extract");
            }

            string modPath = Path.GetFullPath(args[0]);
            string modExtractionFolder;
            if (args.Length >= 2)
            {
                modExtractionFolder = args[1];
            }
            else
            {
                modExtractionFolder = $"{modPath}-extracted";
            }
            modExtractionFolder = Path.GetFullPath(modExtractionFolder);

            Console.WriteLine($"{modPath} -> {modExtractionFolder}");

            if (Directory.Exists(modExtractionFolder))
            {
                Console.Write($"Delete existing extraction folder {modExtractionFolder}? ");
                if (Console.ReadKey().Key == ConsoleKey.Y)
                {
                    Console.WriteLine("\nDeleting folder");
                    Directory.Delete(modExtractionFolder, true);
                }
                else
                {
                    Console.WriteLine("\nNot deleting folder");
                    return;
                }
            }
            Directory.CreateDirectory(modExtractionFolder);
            BinaryReader reader = new BinaryReader(File.OpenRead(modPath));
            Configuration configuration = new Configuration();
            configuration.PreferContiguousImageBuffers = true;

            byte[] header = reader.ReadBytes(4);
            if (header[0] != 'T' || header[1] != 'M' || header[2] != 'O' || header[3] != 'D')
            {
                throw new InvalidDataException($"Signature does not match");
            }
            string version = reader.ReadString();
            byte[] hash = reader.ReadBytes(20);
            byte[] signature = reader.ReadBytes(256);
            uint dataLength = reader.ReadUInt32();
            string modName = reader.ReadString();
            string modVersion = reader.ReadString();
            int fileCount = reader.ReadInt32();
            long totalBytes = 0;

            Console.WriteLine($"Header: {header}");
            Console.WriteLine($"tModLoader version: {version}");
            Console.WriteLine($"Hash: {hash}");
            Console.WriteLine($"Signature: {signature}");
            Console.WriteLine($"Data length: {dataLength}");
            Console.WriteLine($"Mod name: {modName}");
            Console.WriteLine($"Mod version: {modVersion}");
            Console.WriteLine($"File count: {fileCount}");

            string[] fileNames = new string[fileCount];
            int[] uncompressedLengths = new int[fileCount];
            int[] compressedLengths = new int[fileCount];

            for (int i = 0; i < fileCount; i++)
            {
                fileNames[i] = reader.ReadString();
                uncompressedLengths[i] = reader.ReadInt32();
                compressedLengths[i] = reader.ReadInt32();
            }

            for (int i = 0; i < fileCount; i++)
            {
                string fileName = fileNames[i];
                string outputFileName = $"{modExtractionFolder}/{fileName}".Replace(".rawimg", ".png");
                int uncompressedLength = uncompressedLengths[i];
                int compressedLength = compressedLengths[i];

                Console.WriteLine($"File {fileName} -> {outputFileName}, uncompressed {uncompressedLength}, compressed {compressedLength}");

                string directory = Path.GetDirectoryName(outputFileName);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                FileStream stream = File.Create(outputFileName);

                byte[] data = reader.ReadBytes(compressedLength);
                MemoryStream dataStream = new MemoryStream(data);
                BinaryReader dataReader = new BinaryReader(dataStream);

                if (compressedLength != uncompressedLength)
                {
                    DeflateStream deflateStream = new DeflateStream(dataStream, CompressionMode.Decompress);
                    byte[] uncompressedData = new byte[uncompressedLength];
                    deflateStream.Read(uncompressedData, 0, uncompressedData.Length);
                    data = uncompressedData;
                    dataStream = new MemoryStream(data);
                    dataReader = new BinaryReader(dataStream);
                }

                if (fileName.EndsWith(".rawimg"))
                {
                    Console.WriteLine($"Converting rawimg {fileName} to PNG");
                    int imgVersion = dataReader.ReadInt32();
                    if (imgVersion != 1)
                    {
                        throw new InvalidDataException($"{fileName} is version {imgVersion}, only version 1 is supported");
                    }
                    int width = dataReader.ReadInt32();
                    int height = dataReader.ReadInt32();
                    byte[] rawData = dataReader.ReadBytes(width * height * 4);

                    Image<Byte4> image = Image.LoadPixelData<Byte4>(rawData, width, height);
                    PngEncoder encoder = new PngEncoder();
                    encoder.Encode(image, stream);
                }
                else
                {
                    stream.Write(data);
                }

                totalBytes += stream.Length;
            }

            Console.WriteLine($"Done, {totalBytes} bytes in total");
        }
    }
}
