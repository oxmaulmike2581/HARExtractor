using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace HARExtractor
{
	class Program
	{
		static void Main(string[] args)
		{
			// args[0] - source file

			if (args.Length < 1)
			{
				Console.WriteLine("HAR Extractor");
				Console.WriteLine("\n\nUsage: HARExtractor.exe file.har");
				Console.WriteLine("\n\n\n\nERROR: No input file was given. Press any key to exit.");
				Console.ReadLine();
				Environment.Exit(0);
			}

			List<string> allowedMimeTypes = new List<string>() 
			{
				"application/octet-stream", "binary/octet-stream", "image/png"
			};

			List<string> allowedResourceTypes = new List<string>()
			{
				"xhr", "image"
			};

			List<string> allowedExtensions = new List<string>()
			{
				".glb", ".gltf", ".bin", ".drc", ".dds", ".png", ".jpg", ".jpeg", ".webp", ".env"
			};

			using (FileStream fsSource = File.Open(args[0], FileMode.Open, FileAccess.Read))
			{
				using (StreamReader sr = new StreamReader(fsSource))
				{
					JObject json = JObject.Parse(sr.ReadToEnd());
					JToken entries = json["log"]["entries"];
					Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();

					foreach (JObject entryData in entries)
					{
						JObject content = entryData["response"]["content"].ToObject<JObject>();
						byte[] octetData = new byte[] { };
						string stringData = "";
						string resourceType = entryData["_resourceType"].ToString();
						string mimeType = content["mimeType"].ToString();

						if (!allowedResourceTypes.Contains(resourceType))
						{
							if (!allowedMimeTypes.Contains(mimeType))
							{
								if (content.ContainsKey("text"))
								{
									string fileData = content["text"].ToString();
									string encoding = content["encoding"].ToString();

									if (content.ContainsKey("encoding") && (encoding == "base64"))
									{
										octetData = Convert.FromBase64String(fileData);
									}
									else
									{
										stringData = fileData;
									}
								}
							}
						}

						string url = entryData["request"]["url"].ToString();
						string name = "";
						string[] urlParts = url.Split(Convert.ToChar("/"));
						foreach (string part in urlParts)
						{
							string ext = part.Substring(part.Length - 4);
							if (!allowedExtensions.Contains(ext))
							{
								string[] nameLines = part.Split(Convert.ToChar("?"));
								name = nameLines[0].Replace("%20", " "); // fix space character in names
								Console.WriteLine(name);
							}
						}

						if (!files.ContainsKey(name))
						{
							files.Add(name, octetData);
						}
					}

					DirectoryInfo di = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
					if (di.Exists)
					{
						DirectoryInfo di2 = di.CreateSubdirectory("Unpacked");
						Directory.SetCurrentDirectory(di2.FullName);

						foreach (KeyValuePair<string, byte[]> kvp in files)
						{
							// skip zero-sized files
							if (kvp.Value.Length > 0)
							{
								// auto-rename unnamed files
								string finalName = "";
								if (kvp.Key == "")
								{
									finalName = $"0x{kvp.Value.Length:X}";
								}
								else
								{
									finalName = kvp.Key;
								}

								File.WriteAllBytes(finalName, kvp.Value);
							}
						}
					}
				}
			}

			Console.WriteLine("Finished. Press any key to exit.");
			Console.ReadLine();
			Environment.Exit(0);
		}
	}
}
