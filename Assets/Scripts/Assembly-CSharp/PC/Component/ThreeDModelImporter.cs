using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component
{
	/// <summary>
	/// Runtime mesh importer for the 3D printer. No editor-only FBX exporter or
	/// paid plugin is required: FBX/GLB/glTF/OBJ/STL/PLY/3MF are parsed directly
	/// into a Unity Mesh. Materials/animations are intentionally ignored; the
	/// printer only needs geometry, triangle count and bounds.
	/// </summary>
	public static class ThreeDModelImporter
	{
		public static readonly string[] SupportedExtensions =
		{
			".fbx", ".glb", ".gltf", ".obj", ".stl", ".ply", ".3mf"
		};

		private sealed class MeshData
		{
			public readonly List<Vector3> Vertices = new List<Vector3>();
			public readonly List<int> Triangles = new List<int>();

			public void AddFan(List<int> polygon)
			{
				if (polygon == null || polygon.Count < 3) return;
				for (int i = 1; i + 1 < polygon.Count; i++)
				{
					Triangles.Add(polygon[0]);
					Triangles.Add(polygon[i]);
					Triangles.Add(polygon[i + 1]);
				}
			}

			public void Append(MeshData other)
			{
				int offset = Vertices.Count;
				Vertices.AddRange(other.Vertices);
				for (int i = 0; i < other.Triangles.Count; i++)
					Triangles.Add(other.Triangles[i] + offset);
			}
		}

		public static bool IsSupported(string path)
		{
			if (string.IsNullOrEmpty(path)) return false;
			string ext = Path.GetExtension(path).ToLowerInvariant();
			for (int i = 0; i < SupportedExtensions.Length; i++)
				if (SupportedExtensions[i] == ext) return true;
			return false;
		}

		public static PrintableModel Import(string path, Transform parent = null)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				throw new FileNotFoundException("Model file not found", path);
			if (!IsSupported(path))
				throw new NotSupportedException("Unsupported model format: " + Path.GetExtension(path));

			byte[] bytes = File.ReadAllBytes(path);
			string ext = Path.GetExtension(path).ToLowerInvariant();
			string name = Path.GetFileNameWithoutExtension(path);
			MeshData data;

			switch (ext)
			{
				case ".obj": data = ParseObj(bytes); break;
				case ".stl": data = ParseStl(bytes); break;
				case ".ply": data = ParsePly(bytes); break;
				case ".fbx": data = ParseFbx(bytes); break;
				case ".glb":
				case ".gltf": data = ParseGltf(bytes, path, ext == ".glb"); break;
				case ".3mf": data = Parse3mf(bytes); break;
				default: throw new NotSupportedException(ext);
			}

			if (data == null || data.Vertices.Count == 0 || data.Triangles.Count == 0)
				throw new InvalidDataException("No printable triangles found in " + name);

			GameObject go = BuildGameObject(data, name);
			if (parent != null) go.transform.SetParent(parent, false);
			return PrintableModel.FromInstance(go, name);
		}

		private static GameObject BuildGameObject(MeshData data, string name)
		{
			var mesh = new Mesh { name = name };
			mesh.SetVertices(data.Vertices);
			mesh.SetTriangles(data.Triangles, 0);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();

			var go = new GameObject(name);
			var filter = go.AddComponent<MeshFilter>();
			filter.sharedMesh = mesh;
			var renderer = go.AddComponent<MeshRenderer>();
			renderer.sharedMaterial = CreateDefaultMaterial();
			return go;
		}

		private static Material CreateDefaultMaterial()
		{
			Shader shader = Shader.Find("Universal Render Pipeline/Lit");
			if (shader == null) shader = Shader.Find("Standard");
			if (shader == null) shader = Shader.Find("Diffuse");
			var material = new Material(shader);
			material.color = new Color(1f, 0.55f, 0.15f, 1f);
			return material;
		}

		// ----- OBJ ------------------------------------------------------------

		private static MeshData ParseObj(byte[] bytes)
		{
			var data = new MeshData();
			var polygon = new List<int>();
			using (var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					line = line.Trim();
					if (line.Length == 0 || line[0] == '#') continue;
					string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length == 0) continue;

					if (parts[0] == "v" && parts.Length >= 4)
					{
						data.Vertices.Add(new Vector3(
							ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])));
					}
					else if (parts[0] == "f")
					{
						polygon.Clear();
						for (int i = 1; i < parts.Length; i++)
						{
							string token = parts[i].Split('/')[0];
							if (!int.TryParse(token, out int index)) continue;
							int zeroBased = index > 0 ? index - 1 : data.Vertices.Count + index;
							if (zeroBased >= 0 && zeroBased < data.Vertices.Count)
								polygon.Add(zeroBased);
						}
						data.AddFan(polygon);
					}
				}
			}
			return data;
		}

		// ----- STL ------------------------------------------------------------

		private static MeshData ParseStl(byte[] bytes)
		{
			if (IsBinaryStl(bytes)) return ParseBinaryStl(bytes);
			return ParseAsciiStl(bytes);
		}

		private static bool IsBinaryStl(byte[] bytes)
		{
			if (bytes.Length < 84) return false;
			uint triangles = BitConverter.ToUInt32(bytes, 80);
			long expected = 84L + triangles * 50L;
			if (expected == bytes.Length) return true;
			string head = Encoding.ASCII.GetString(bytes, 0, Math.Min(80, bytes.Length));
			return !head.TrimStart().StartsWith("solid", StringComparison.OrdinalIgnoreCase);
		}

		private static MeshData ParseBinaryStl(byte[] bytes)
		{
			var data = new MeshData();
			uint triangles = BitConverter.ToUInt32(bytes, 80);
			int offset = 84;
			for (int t = 0; t < triangles; t++)
			{
				offset += 12; // normal
				int baseIndex = data.Vertices.Count;
				for (int v = 0; v < 3; v++)
				{
					data.Vertices.Add(new Vector3(
						BitConverter.ToSingle(bytes, offset),
						BitConverter.ToSingle(bytes, offset + 4),
						BitConverter.ToSingle(bytes, offset + 8)));
					offset += 12;
				}
				offset += 2; // attribute
				data.Triangles.Add(baseIndex);
				data.Triangles.Add(baseIndex + 1);
				data.Triangles.Add(baseIndex + 2);
			}
			return data;
		}

		private static MeshData ParseAsciiStl(byte[] bytes)
		{
			var data = new MeshData();
			var face = new List<int>();
			using (var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					line = line.Trim();
					if (!line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase)) continue;
					string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length < 4) continue;
					data.Vertices.Add(new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])));
					face.Add(data.Vertices.Count - 1);
					if (face.Count == 3)
					{
						data.Triangles.AddRange(face);
						face.Clear();
					}
				}
			}
			return data;
		}

		// ----- PLY ------------------------------------------------------------

		private static MeshData ParsePly(byte[] bytes)
		{
			int offset = 0;
			string format = "ascii";
			int vertexCount = 0, faceCount = 0;
			var vertexProps = new List<KeyValuePair<string, string>>();
			string faceCountType = "uchar";
			string faceIndexType = "int";
			bool inVertex = false, inFace = false;

			while (offset < bytes.Length)
			{
				string line = ReadAsciiLine(bytes, ref offset);
				if (line == null) break;
				string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0) continue;
				if (parts[0] == "format" && parts.Length >= 2) format = parts[1];
				else if (parts[0] == "element" && parts.Length >= 3)
				{
					inVertex = parts[1] == "vertex";
					inFace = parts[1] == "face";
					if (inVertex) vertexCount = int.Parse(parts[2]);
					if (inFace) faceCount = int.Parse(parts[2]);
				}
				else if (parts[0] == "property" && inVertex && parts.Length >= 3)
					vertexProps.Add(new KeyValuePair<string, string>(parts[1], parts[2]));
				else if (parts[0] == "property" && inFace && parts.Length >= 5 && parts[1] == "list")
				{
					faceCountType = parts[2];
					faceIndexType = parts[3];
				}
				else if (parts[0] == "end_header") break;
			}

			var data = new MeshData();
			if (format == "ascii")
			{
				int xIndex = PropIndex(vertexProps, "x");
				int yIndex = PropIndex(vertexProps, "y");
				int zIndex = PropIndex(vertexProps, "z");
				using (var reader = new StreamReader(new MemoryStream(bytes, offset, bytes.Length - offset), Encoding.UTF8))
				{
					for (int i = 0; i < vertexCount; i++)
					{
						string line = reader.ReadLine();
						if (line == null) break;
						string[] p = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
						data.Vertices.Add(new Vector3(ParseFloat(p[xIndex]), ParseFloat(p[yIndex]), ParseFloat(p[zIndex])));
					}
					var polygon = new List<int>();
					for (int i = 0; i < faceCount; i++)
					{
						string line = reader.ReadLine();
						if (line == null) break;
						string[] p = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
						if (p.Length == 0) continue;
						int count = int.Parse(p[0]);
						polygon.Clear();
						for (int j = 0; j < count && j + 1 < p.Length; j++)
							polygon.Add(int.Parse(p[j + 1]));
						data.AddFan(polygon);
					}
				}
				return data;
			}

			if (format != "binary_little_endian")
				throw new NotSupportedException("Only ascii and binary_little_endian PLY are supported");

			int stride = 0;
			int xOff = -1, yOff = -1, zOff = -1;
			string xType = "float", yType = "float", zType = "float";
			for (int i = 0; i < vertexProps.Count; i++)
			{
				if (vertexProps[i].Value == "x") { xOff = stride; xType = vertexProps[i].Key; }
				if (vertexProps[i].Value == "y") { yOff = stride; yType = vertexProps[i].Key; }
				if (vertexProps[i].Value == "z") { zOff = stride; zType = vertexProps[i].Key; }
				stride += PlyTypeSize(vertexProps[i].Key);
			}
			for (int i = 0; i < vertexCount; i++)
			{
				int baseOffset = offset + i * stride;
				data.Vertices.Add(new Vector3(
					(float)ReadPlyValue(bytes, baseOffset + xOff, xType),
					(float)ReadPlyValue(bytes, baseOffset + yOff, yType),
					(float)ReadPlyValue(bytes, baseOffset + zOff, zType)));
			}
			offset += vertexCount * stride;
			var poly = new List<int>();
			for (int i = 0; i < faceCount; i++)
			{
				int count = (int)ReadPlyValue(bytes, offset, faceCountType);
				offset += PlyTypeSize(faceCountType);
				poly.Clear();
				for (int j = 0; j < count; j++)
				{
					poly.Add((int)ReadPlyValue(bytes, offset, faceIndexType));
					offset += PlyTypeSize(faceIndexType);
				}
				data.AddFan(poly);
			}
			return data;
		}

		private static int PropIndex(List<KeyValuePair<string, string>> props, string name)
		{
			for (int i = 0; i < props.Count; i++)
				if (props[i].Value == name) return i;
			throw new InvalidDataException("PLY vertex property missing: " + name);
		}

		private static int PlyTypeSize(string type)
		{
			switch (type)
			{
				case "char": case "uchar": case "int8": case "uint8": return 1;
				case "short": case "ushort": case "int16": case "uint16": return 2;
				case "int": case "uint": case "int32": case "uint32": case "float": case "float32": return 4;
				case "double": case "float64": return 8;
				default: throw new NotSupportedException("PLY type " + type);
			}
		}

		private static double ReadPlyValue(byte[] bytes, int offset, string type)
		{
			switch (type)
			{
				case "char": case "int8": return (sbyte)bytes[offset];
				case "uchar": case "uint8": return bytes[offset];
				case "short": case "int16": return BitConverter.ToInt16(bytes, offset);
				case "ushort": case "uint16": return BitConverter.ToUInt16(bytes, offset);
				case "int": case "int32": return BitConverter.ToInt32(bytes, offset);
				case "uint": case "uint32": return BitConverter.ToUInt32(bytes, offset);
				case "float": case "float32": return BitConverter.ToSingle(bytes, offset);
				case "double": case "float64": return BitConverter.ToDouble(bytes, offset);
				default: throw new NotSupportedException(type);
			}
		}

		// ----- FBX ------------------------------------------------------------

		private static MeshData ParseFbx(byte[] bytes)
		{
			var data = new MeshData();
			if (bytes.Length > 23 && Encoding.ASCII.GetString(bytes, 0, 18) == "Kaydara FBX Binary")
			{
				var reader = new FbxBinaryReader(bytes);
				var root = reader.ReadRoot();
				CollectFbxGeometry(root, data);
			}
			else
			{
				string text = Encoding.UTF8.GetString(bytes);
				var vertexMatches = Regex.Matches(text, @"Vertices:\s*\*\d+\s*\{\s*a:\s*([^}]*)\}");
				var indexMatches = Regex.Matches(text, @"PolygonVertexIndex:\s*\*\d+\s*\{\s*a:\s*([^}]*)\}");
				int count = Math.Min(vertexMatches.Count, indexMatches.Count);
				for (int i = 0; i < count; i++)
				{
					var local = new MeshData();
					var raw = new List<double>();
					foreach (string token in vertexMatches[i].Groups[1].Value.Split(','))
						if (double.TryParse(token.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
							raw.Add(v);
					local.Vertices.Clear();
					for (int v = 0; v + 2 < raw.Count; v += 3)
						local.Vertices.Add(new Vector3((float)raw[v], (float)raw[v + 1], (float)raw[v + 2]));

					var polygon = new List<int>();
					foreach (string token in indexMatches[i].Groups[1].Value.Split(','))
					{
						if (!int.TryParse(token.Trim(), out int rawIndex)) continue;
						int index = rawIndex < 0 ? -rawIndex - 1 : rawIndex;
						polygon.Add(index);
						if (rawIndex < 0)
						{
							local.AddFan(polygon);
							polygon.Clear();
						}
					}
					data.Append(local);
				}
			}
			return data;
		}

		private static void CollectFbxGeometry(FbxNode node, MeshData data)
		{
			if (node == null) return;
			if (node.Name == "Geometry")
			{
				double[] vertices = GetFbxDoubleArray(node, "Vertices");
				int[] indices = GetFbxIntArray(node, "PolygonVertexIndex");
				if (vertices != null && indices != null)
				{
					var local = new MeshData();
					for (int i = 0; i + 2 < vertices.Length; i += 3)
						local.Vertices.Add(new Vector3((float)vertices[i], (float)vertices[i + 1], (float)vertices[i + 2]));
					var polygon = new List<int>();
					for (int i = 0; i < indices.Length; i++)
					{
						int raw = indices[i];
						int index = raw < 0 ? -raw - 1 : raw;
						if (index >= 0 && index < local.Vertices.Count) polygon.Add(index);
						if (raw < 0)
						{
							local.AddFan(polygon);
							polygon.Clear();
						}
					}
					data.Append(local);
				}
			}
			for (int i = 0; i < node.Children.Count; i++)
				CollectFbxGeometry(node.Children[i], data);
		}

		private static double[] GetFbxDoubleArray(FbxNode parent, string name)
		{
			var child = parent.Find(name);
			if (child == null || child.Properties.Count == 0) return null;
			object prop = child.Properties[0];
			if (prop is double[] d) return d;
			if (prop is float[] f)
			{
				var result = new double[f.Length];
				for (int i = 0; i < f.Length; i++) result[i] = f[i];
				return result;
			}
			return null;
		}

		private static int[] GetFbxIntArray(FbxNode parent, string name)
		{
			var child = parent.Find(name);
			if (child == null || child.Properties.Count == 0) return null;
			object prop = child.Properties[0];
			if (prop is int[] i) return i;
			if (prop is long[] l)
			{
				var result = new int[l.Length];
				for (int j = 0; j < l.Length; j++) result[j] = (int)l[j];
				return result;
			}
			return null;
		}

		private sealed class FbxNode
		{
			public string Name;
			public readonly List<object> Properties = new List<object>();
			public readonly List<FbxNode> Children = new List<FbxNode>();

			public FbxNode Find(string name)
			{
				for (int i = 0; i < Children.Count; i++)
					if (Children[i].Name == name) return Children[i];
				return null;
			}
		}

		private sealed class FbxBinaryReader
		{
			private readonly byte[] bytes;
			private readonly bool longOffsets;
			private int position;

			public FbxBinaryReader(byte[] bytes)
			{
				this.bytes = bytes;
				int version = BitConverter.ToInt32(bytes, 23);
				longOffsets = version >= 7500;
				position = 27;
			}

			public FbxNode ReadRoot()
			{
				var root = new FbxNode { Name = "Root" };
				while (position < bytes.Length)
				{
					var node = ReadNode();
					if (node == null) break;
					root.Children.Add(node);
				}
				return root;
			}

			private FbxNode ReadNode()
			{
				int headerSize = longOffsets ? 25 : 13;
				if (position + headerSize > bytes.Length) return null;
				long endOffset = longOffsets ? (long)BitConverter.ToUInt64(bytes, position) : BitConverter.ToUInt32(bytes, position);
				long propertyCount = longOffsets ? (long)BitConverter.ToUInt64(bytes, position + 8) : BitConverter.ToUInt32(bytes, position + 4);
				int nameLength = bytes[position + (longOffsets ? 24 : 12)];
				if (endOffset == 0)
				{
					position += headerSize;
					return null;
				}
				position += headerSize;
				var node = new FbxNode
				{
					Name = Encoding.ASCII.GetString(bytes, position, nameLength)
				};
				position += nameLength;
				for (long i = 0; i < propertyCount; i++)
					node.Properties.Add(ReadProperty());
				while (position < endOffset - headerSize)
				{
					var child = ReadNode();
					if (child == null) break;
					node.Children.Add(child);
				}
				position = (int)endOffset;
				return node;
			}

			private object ReadProperty()
			{
				char type = (char)bytes[position++];
				switch (type)
				{
					case 'Y': { short v = BitConverter.ToInt16(bytes, position); position += 2; return v; }
					case 'C': { bool v = bytes[position] != 0; position += 1; return v; }
					case 'I': { int v = BitConverter.ToInt32(bytes, position); position += 4; return v; }
					case 'F': { float v = BitConverter.ToSingle(bytes, position); position += 4; return v; }
					case 'D': { double v = BitConverter.ToDouble(bytes, position); position += 8; return v; }
					case 'L': { long v = BitConverter.ToInt64(bytes, position); position += 8; return v; }
					case 'S':
					case 'R':
					{
						int length = BitConverter.ToInt32(bytes, position); position += 4;
						if (type == 'S')
						{
							string s = Encoding.UTF8.GetString(bytes, position, length);
							position += length;
							return s;
						}
						var raw = new byte[length];
						Array.Copy(bytes, position, raw, 0, length);
						position += length;
						return raw;
					}
					case 'f': return ReadArray<float>(4, (b, o) => BitConverter.ToSingle(b, o));
					case 'd': return ReadArray<double>(8, (b, o) => BitConverter.ToDouble(b, o));
					case 'l': return ReadArray<long>(8, (b, o) => BitConverter.ToInt64(b, o));
					case 'i': return ReadArray<int>(4, (b, o) => BitConverter.ToInt32(b, o));
					case 'b': return ReadArray<byte>(1, (b, o) => b[o]);
					default:
						throw new NotSupportedException("FBX property type " + type);
				}
			}

			private T[] ReadArray<T>(int elementSize, Func<byte[], int, T> read)
			{
				int length = BitConverter.ToInt32(bytes, position); position += 4;
				int encoding = BitConverter.ToInt32(bytes, position); position += 4;
				int compressedLength = BitConverter.ToInt32(bytes, position); position += 4;
				byte[] raw;
				if (encoding == 0)
				{
					raw = new byte[compressedLength];
					Array.Copy(bytes, position, raw, 0, compressedLength);
					position += compressedLength;
				}
				else
				{
					raw = new byte[length * elementSize];
					// zlib wrapper: skip 2-byte header before raw deflate.
					using (var deflate = new DeflateStream(new MemoryStream(bytes, position + 2, compressedLength - 2), CompressionMode.Decompress))
					{
						int read = 0;
						while (read < raw.Length)
						{
							int n = deflate.Read(raw, read, raw.Length - read);
							if (n <= 0) break;
							read += n;
						}
					}
					position += compressedLength;
				}
				var result = new T[length];
				for (int i = 0; i < length; i++) result[i] = read(raw, i * elementSize);
				return result;
			}
		}

		// ----- glTF / GLB -----------------------------------------------------

		private static MeshData ParseGltf(byte[] bytes, string path, bool isGlb)
		{
			string json;
			byte[] glbBin = null;
			if (isGlb)
			{
				if (bytes.Length < 12) throw new InvalidDataException("Invalid GLB");
				int offset = 12;
				string jsonText = null;
				while (offset + 8 <= bytes.Length)
				{
					int chunkLength = BitConverter.ToInt32(bytes, offset);
					uint chunkType = BitConverter.ToUInt32(bytes, offset + 4);
					offset += 8;
					if (chunkType == 0x4E4F534A) jsonText = Encoding.UTF8.GetString(bytes, offset, chunkLength);
					else if (chunkType == 0x004E4942)
					{
						glbBin = new byte[chunkLength];
						Array.Copy(bytes, offset, glbBin, 0, chunkLength);
					}
					offset += chunkLength;
				}
				if (jsonText == null) throw new InvalidDataException("GLB JSON chunk missing");
				json = jsonText;
			}
			else json = Encoding.UTF8.GetString(bytes);

			var gltf = JObject.Parse(json);
			var meshes = gltf["meshes"] as JArray;
			if (meshes == null || meshes.Count == 0) throw new InvalidDataException("glTF has no meshes");
			var bufferCache = new Dictionary<int, byte[]>();
			string dir = Path.GetDirectoryName(path) ?? "";
			var data = new MeshData();

			for (int m = 0; m < meshes.Count; m++)
			{
				var primitives = meshes[m]["primitives"] as JArray;
				if (primitives == null) continue;
				for (int p = 0; p < primitives.Count; p++)
				{
					var primitive = primitives[p];
					int mode = primitive["mode"] != null ? (int)primitive["mode"] : 4;
					if (mode != 4) continue; // triangles only
					var attributes = primitive["attributes"] as JObject;
					if (attributes == null || attributes["POSITION"] == null) continue;
					int positionAccessor = (int)attributes["POSITION"];
					float[] positions = ReadGltfFloatAccessor(gltf, positionAccessor, glbBin, dir, bufferCache, 3);
					int vertexOffset = data.Vertices.Count;
					for (int i = 0; i + 2 < positions.Length; i += 3)
						data.Vertices.Add(new Vector3(positions[i], positions[i + 1], positions[i + 2]));

					if (primitive["indices"] != null)
					{
						int[] indices = ReadGltfIntAccessor(gltf, (int)primitive["indices"], glbBin, dir, bufferCache);
						for (int i = 0; i + 2 < indices.Length; i += 3)
						{
							data.Triangles.Add(indices[i] + vertexOffset);
							data.Triangles.Add(indices[i + 1] + vertexOffset);
							data.Triangles.Add(indices[i + 2] + vertexOffset);
						}
					}
					else
					{
						int count = positions.Length / 3;
						for (int i = 0; i + 2 < count; i += 3)
						{
							data.Triangles.Add(i + vertexOffset);
							data.Triangles.Add(i + 1 + vertexOffset);
							data.Triangles.Add(i + 2 + vertexOffset);
						}
					}
				}
			}
			return data;
		}

		private static byte[] GetGltfBuffer(JObject gltf, int index, byte[] glbBin, string dir, Dictionary<int, byte[]> cache)
		{
			if (cache.TryGetValue(index, out byte[] cached)) return cached;
			var buffers = gltf["buffers"] as JArray;
			if (buffers == null || index < 0 || index >= buffers.Count) throw new InvalidDataException("glTF buffer missing");
			string uri = (string)buffers[index]["uri"];
			byte[] result;
			if (string.IsNullOrEmpty(uri))
			{
				if (glbBin == null) throw new InvalidDataException("GLB BIN chunk missing");
				result = glbBin;
			}
			else if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
			{
				int comma = uri.IndexOf(',');
				result = Convert.FromBase64String(uri.Substring(comma + 1));
			}
			else
			{
				string file = Path.Combine(dir, Uri.UnescapeDataString(uri));
				result = File.ReadAllBytes(file);
			}
			cache[index] = result;
			return result;
		}

		private static float[] ReadGltfFloatAccessor(JObject gltf, int accessorIndex, byte[] glbBin, string dir, Dictionary<int, byte[]> cache, int components)
		{
			var accessors = gltf["accessors"] as JArray;
			var accessor = accessors[accessorIndex] as JObject;
			int count = (int)accessor["count"];
			int accessorOffset = accessor["byteOffset"] != null ? (int)accessor["byteOffset"] : 0;
			int bufferViewIndex = (int)accessor["bufferView"];
			var bufferViews = gltf["bufferViews"] as JArray;
			var view = bufferViews[bufferViewIndex] as JObject;
			int viewOffset = view["byteOffset"] != null ? (int)view["byteOffset"] : 0;
			int byteStride = view["byteStride"] != null ? (int)view["byteStride"] : 0;
			int stride = byteStride > 0 ? byteStride : components * 4;
			byte[] buffer = GetGltfBuffer(gltf, (int)view["buffer"], glbBin, dir, cache);
			int offset = viewOffset + accessorOffset;
			var result = new float[count * components];
			for (int i = 0; i < count; i++)
				for (int c = 0; c < components; c++)
					result[i * components + c] = BitConverter.ToSingle(buffer, offset + i * stride + c * 4);
			return result;
		}

		private static int[] ReadGltfIntAccessor(JObject gltf, int accessorIndex, byte[] glbBin, string dir, Dictionary<int, byte[]> cache)
		{
			var accessors = gltf["accessors"] as JArray;
			var accessor = accessors[accessorIndex] as JObject;
			int componentType = (int)accessor["componentType"];
			int count = (int)accessor["count"];
			int accessorOffset = accessor["byteOffset"] != null ? (int)accessor["byteOffset"] : 0;
			int bufferViewIndex = (int)accessor["bufferView"];
			var bufferViews = gltf["bufferViews"] as JArray;
			var view = bufferViews[bufferViewIndex] as JObject;
			int viewOffset = view["byteOffset"] != null ? (int)view["byteOffset"] : 0;
			byte[] buffer = GetGltfBuffer(gltf, (int)view["buffer"], glbBin, dir, cache);
			int offset = viewOffset + accessorOffset;
			var result = new int[count];
			for (int i = 0; i < count; i++)
			{
				if (componentType == 5125) result[i] = (int)BitConverter.ToUInt32(buffer, offset + i * 4);
				else if (componentType == 5123) result[i] = BitConverter.ToUInt16(buffer, offset + i * 2);
				else result[i] = buffer[offset + i];
			}
			return result;
		}

		// ----- 3MF ------------------------------------------------------------

		private static MeshData Parse3mf(byte[] bytes)
		{
			var data = new MeshData();
			using (var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
			{
				ZipArchiveEntry entry = null;
				foreach (var e in archive.Entries)
				{
					if (e.FullName.EndsWith("3dmodel.model", StringComparison.OrdinalIgnoreCase))
					{
						entry = e;
						break;
					}
				}
				if (entry == null) throw new InvalidDataException("3MF model part missing");
				XDocument doc;
				using (var stream = entry.Open()) doc = XDocument.Load(stream);
				foreach (var mesh in doc.Descendants().Where(d => d.Name.LocalName == "mesh"))
				{
					var local = new MeshData();
					foreach (var vertex in mesh.Descendants().Where(d => d.Name.LocalName == "vertex"))
					{
						local.Vertices.Add(new Vector3(
							ParseFloat((string)vertex.Attribute("x")),
							ParseFloat((string)vertex.Attribute("y")),
							ParseFloat((string)vertex.Attribute("z"))));
					}
					foreach (var triangle in mesh.Descendants().Where(d => d.Name.LocalName == "triangle"))
					{
						int v1 = int.Parse((string)triangle.Attribute("v1"));
						int v2 = int.Parse((string)triangle.Attribute("v2"));
						int v3 = int.Parse((string)triangle.Attribute("v3"));
						if (v1 < local.Vertices.Count && v2 < local.Vertices.Count && v3 < local.Vertices.Count)
						{
							local.Triangles.Add(v1);
							local.Triangles.Add(v2);
							local.Triangles.Add(v3);
						}
					}
					data.Append(local);
				}
			}
			return data;
		}

		// ----- helpers --------------------------------------------------------

		private static float ParseFloat(string text)
		{
			return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
		}

		private static string ReadAsciiLine(byte[] bytes, ref int offset)
		{
			if (offset >= bytes.Length) return null;
			int start = offset;
			while (offset < bytes.Length && bytes[offset] != (byte)'\n') offset++;
			int length = offset - start;
			if (offset < bytes.Length) offset++; // skip \n
			string line = Encoding.ASCII.GetString(bytes, start, length);
			return line.TrimEnd('\r');
		}
	}
}
