using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;

namespace HARExtractor
{
	class Program
	{
		private static readonly List<string> allowedMimeTypes = new List<string>()
		{
			"application/octet-stream", "binary/octet-stream", "image/png", "image/jpeg",
			"model/gltf+json", "audio/mp3", "image/webp"
		};

		private static readonly List<string> allowedResourceTypes = new List<string>()
		{
			"xhr", "image"
		};

		static void Main(string[] args)
		{
			// First of all, we display the welcome message
			// And if this tool was launched without arguments - a help message with usage.
			if (args.Length < 1)
			{
				Console.WriteLine("HAR Extractor");
				Console.WriteLine("\n\nUsage: HARExtractor.exe file.har");
				Console.WriteLine("\n\n\n\nERROR: No input file was given. Press any key to exit.");
				Console.ReadLine();
				Environment.Exit(0);
			}

			using (FileStream fsSource = File.Open(args[0], FileMode.Open, FileAccess.Read))
			{
				using (StreamReader sr = new StreamReader(fsSource))
				{
					// First, we should parse our file (.har is a specific json) and get the "entries" sub-array
					JObject json = JObject.Parse(sr.ReadToEnd());
					JToken entries = json["log"]["entries"];

					// Next we should create a main storage which will used for extracted files
					// key = name, value = data
					Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
					Dictionary<string, string> plainFiles = new Dictionary<string, string>();

					// And here we process our files
					foreach (JObject entryData in entries)
					{
						// used for easily navigation
						JObject content = entryData["response"]["content"].ToObject<JObject>();

						// data storages: first for binary data, second for text data;
						byte[] octetData = Array.Empty<byte>();
						string stringData = string.Empty;

						// meta information: resource type, MIME type, file size, file name
						string resourceType = entryData["_resourceType"].ToString();
						string mimeType     = content["mimeType"].ToString();
						uint fileSize = Convert.ToUInt32(content["size"].ToString());
						string fileName = "";

						// if our resource type is allowed, then continue
						if (allowedResourceTypes.Contains(resourceType))
						{
							// if our MIME type is allowed, then continue
							if (allowedMimeTypes.Contains(mimeType))
							{
								// Get a file name
								fileName = ExtractFileNameFromURL(entryData["request"]["url"].ToString());

								// if we have an empty name, just auto-rename it
								if (fileName == string.Empty)
								{
									fileName = $"0x{fileSize:X}";
								}

								// if our file is internal (e.g. stored inside .har), then continue
								if (content.ContainsKey("text"))
								{
									// read file data (plain or encoded)
									string fileData = content["text"].ToString();

									// if we find an "encoding" field, then we try to decode it
									if (content.ContainsKey("encoding"))
									{
										// Now we define the encoding. In most cases it will be a base64.
										string encoding = content["encoding"].ToString();

										if (encoding == "base64")
										{
											// just decode it and store as byte array
											octetData = Convert.FromBase64String(fileData);
										}
									}
									else
									{
										// just store it "as is". useful for shaders, svg images, etc.
										stringData = fileData;
									}
								}
								else
								{
									// if our file is external (e.g. we have only URL, not the data)
									// we print a warning message
									// of course, yes, we could try to download it
									// but who needs scripts or web fonts?
									Console.WriteLine($"WARN: {fileName} has been skipped because of external.");
								}
							}
						}

						// we check it to avoid duplicates
						if (!files.ContainsKey(fileName))
						{
							// store it
							files.Add(fileName, octetData);
						}

						// And here do the same as previous
						if (!plainFiles.ContainsKey(fileName))
						{
							plainFiles.Add(fileName, stringData);
						}
					}

					// Now we get a current directory
					DirectoryInfo di = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
					if (di.Exists)
					{
						// Then we create a sub-directory called "Unpacked"
						// And set it as current working directory
						DirectoryInfo di2 = di.CreateSubdirectory("Unpacked");
						Directory.SetCurrentDirectory(di2.FullName);

						// Finally, we write our files on disk
						WriteAllFiles(files);
						WriteAllFiles(plainFiles);
					}
				}
			}

			// Finally, we inform you that all operations have been successfully completed
			// And tells you what you can do to exit this tool.
			Console.WriteLine("Finished. Press any key to exit.");
			Console.ReadLine();
			Environment.Exit(0);
		}

		static string ExtractFileNameFromURL(string url)
		{
			// First, we remove all protocols
			List<string> protocols = new List<string>() { "https://", "http://", "ftp://" };
			string cleanedUrl = string.Empty;

			foreach (string protocol in protocols)
			{
				cleanedUrl = url.Replace(protocol, "");
			}

			// Then, we split our url by separator to remove unneccessary parts
			// and extract our file name
			string[] splittedURL = cleanedUrl.Split(Convert.ToChar("/"));
			string fullFileName = splittedURL.Last();

			// Finally just return our name
			return CleanFileName(fullFileName);
		}

		static void WriteAllFiles(Dictionary<string, byte[]> filesToWrite)
		{
			foreach (KeyValuePair<string, byte[]> kvp in filesToWrite)
			{
				// skip zero-sized files
				if (kvp.Value.Length > 0)
				{
					File.WriteAllBytes(kvp.Key, kvp.Value);
					Console.WriteLine($"Write {kvp.Key} ({kvp.Value.Length} bytes) - OK");
				}
			}
		}

		static void WriteAllFiles(Dictionary<string, string> filesToWrite)
		{
			foreach (KeyValuePair<string, string> kvp in filesToWrite)
			{
				// skip zero-sized files
				if (kvp.Value.Length > 0)
				{
					File.WriteAllText(kvp.Key, kvp.Value);
					Console.WriteLine($"Write {kvp.Key} ({kvp.Value.Length} bytes) - OK");
				}
			}
		}

		static string CleanFileName(string fileName)
		{
			// Now we should clear our file name from timestamps and some other URL parameters
			// e.g. convert "file.glb?t=12312414" to "file.glb" (for example)
			string[] splittedName = fileName.Split(Convert.ToChar("?"));
			string encodedFileName = splittedName.First();

			// Then, we need to fix our name, namely - replace all web mnemonic codes to corresponding characters.
			string decodedFileName = WebUtility.UrlDecode(encodedFileName);

			return decodedFileName;
		}
	}
}
