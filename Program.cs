using System.Collections.Concurrent;
using System.Text;

namespace AcropalypseScanner
{
    public class Program
    {
        private static int NumberOfFilesProcessed = 0;
        private static ConcurrentBag<string> results = new();

        public static async Task<int> Main(string[] args)
        {
            Console.WriteLine("Enumerating files");

            var files = Directory.EnumerateFiles(args[0], "*.png", new EnumerationOptions { RecurseSubdirectories = true });

            Console.CursorLeft = 0;
            Console.Write("Files processed: " + NumberOfFilesProcessed + " / " + files.Count());

            var task = Parallel.ForEachAsync(files, new ParallelOptions() { MaxDegreeOfParallelism = 30 }, async (filename, token) => { await AnalysisTask(filename, token); });

            while (!task.IsCompleted)
            {
                await Task.Delay(250);

                Console.CursorLeft = 0;
                Console.Write("Files processed: " + NumberOfFilesProcessed + " / " + files.Count());  
            }
            Console.WriteLine("All files processed");

            foreach (var msg in results)
            {
                Console.WriteLine(msg);
            }

            return 0;
        }

        private static async Task AnalysisTask(string filename, CancellationToken token)
        {
            try
            {
                using (var fs = File.OpenRead(filename))
                {
                    byte[] buffer = new byte[8];

                    // set the position to the end of the 
                    var read = await fs.ReadAsync(buffer);
                    if (read != buffer.Length)
                    {
                        throw new Exception("Invalid read");
                    }

                    if (Encoding.UTF8.GetString(buffer) == "%PNG")
                    {
                        throw new Exception("Not a PNG");
                    }

                    // loop through each PNG chunk and check to see if its the end chunk
                    while (fs.Position < fs.Length && !token.IsCancellationRequested)
                    {
                        byte[] lengthBuffer = new byte[4];
                        if (await fs.ReadAsync(lengthBuffer) != lengthBuffer.Length)
                        {
                            throw new Exception("Invalid length read");
                        }
                        lengthBuffer = lengthBuffer.Reverse().ToArray();
                        var length = BitConverter.ToUInt32(lengthBuffer, 0) + 4; // +4 crc, we've alreay read the length

                        byte[] chunkType = new byte[4];
                        read = await fs.ReadAsync(chunkType);
                        if (read != chunkType.Length)
                        {
                            throw new Exception("Invalid chunk read: " + read);
                        }

                        if (Encoding.UTF8.GetString(chunkType) == "IEND")
                        {
                            var realPNGLength = fs.Position + 4;

                            if (realPNGLength < fs.Length)
                            {
                                results.Add("Trucated PNG found: " + filename);
                            }
                            Interlocked.Increment(ref NumberOfFilesProcessed);
                            break;
                        }
                        else
                        {
                            fs.Position += length;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add(filename + " - " + ex.Message);
            }
        }
    }
}