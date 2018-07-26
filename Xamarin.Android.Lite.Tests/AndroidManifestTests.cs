﻿using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Xamarin.Android.Lite.Tasks;

namespace Xamarin.Android.Lite.Tests
{
	[TestFixture]
	public class AndroidManifestTests
	{
		Stream binaryManifest, textManifest;

		[SetUp]
		public void SetUp ()
		{
			var path = Path.Combine (Path.GetDirectoryName (GetType ().Assembly.Location), "Data");
			textManifest = File.OpenRead (Path.Combine (path, "AndroidManifest.xml"));
			binaryManifest = File.OpenRead (Path.Combine (path, "AndroidManifest.xml.bin"));
		}

		[TearDown]
		public void TearDown ()
		{
			textManifest.Dispose ();
			binaryManifest.Dispose ();
		}

		/// <summary>
		/// NOTE: these values are generated by aapt, so we have to "fixup" the manifest a bit to match the binary one
		/// </summary>
		string LoadText ()
		{
			var ns = XNamespace.Get ("http://schemas.android.com/apk/res/android");
			var xml = XElement.Load (textManifest);
			xml.SetAttributeValue ("platformBuildVersionCode", "27");
			xml.SetAttributeValue ("platformBuildVersionName", "8.1.0");

			//HACK: replace string resource names with integers
			const int mipmap_icon     = 2130903040; // @mipmap/icon
			const int style_MainTheme = 2131493263; // @style/MainTheme

			var application = xml.Element ("application");
			application.SetAttributeValue (ns + "icon", mipmap_icon);

			var activity = application.Element ("activity");
			activity.SetAttributeValue (ns + "theme", style_MainTheme);
			activity.SetAttributeValue (ns + "icon", mipmap_icon);
			activity.SetAttributeValue (ns + "configChanges", 1152); //orientation|screenSize

			return xml.ToString ();
		}

		[Test]
		public void ReadManifest ()
		{
			var doc = AndroidManifest.Read (binaryManifest);
			var xmlFromBinary = doc.Document.ToString ();
			var xmlFromText = LoadText ();

			Assert.IsTrue (doc.Strings?.Count > 0, "Strings should be non-empty!");
			Assert.IsTrue (doc.Resources?.Count > 0, "Resources should be non-empty!");
			Assert.IsFalse (string.IsNullOrEmpty (doc.PlatformBuildVersionName), "FileVersion should be non-empty!");
			Assert.AreEqual (xmlFromText, xmlFromBinary);
		}

		/// <summary>
		/// NOTE: strings are not ordered the same
		/// </summary>
		void ContainsAllStrings (IList<string> expected, IList<string> actual)
		{
			var builder = new StringBuilder ();
			foreach (var @string in expected) {
				if (!actual.Contains (@string))
					builder.AppendLine ($"Does not contain `{@string}`.");
			}
			if (builder.Length > 0)
				Assert.Fail (builder.ToString ());
			Assert.AreEqual (expected.Count, actual.Count, "Strings lengths should match!");
		}

		[Test]
		public void WriteManifest ()
		{
			var expectedChunks = new StringBuilder ();
			var expectedDoc = AndroidManifest.Read (binaryManifest, (t, c, p) => expectedChunks.AppendLine($"{t}, chunkSize: {c}, position: {p}"));
			var expectedStrings = expectedDoc.Strings;
			var expectedResources = expectedDoc.Resources;
			var expectedFileVersion = expectedDoc.PlatformBuildVersionName;

			var stream = new MemoryStream ();
			expectedDoc.Write (stream);

			//Compare the string tables
			var actualStrings = expectedDoc.Strings;
			var actualResources = expectedDoc.Resources;
			var actualFileVersion = expectedDoc.PlatformBuildVersionName;
			ContainsAllStrings (expectedStrings, actualStrings);
			Assert.AreEqual (expectedResources, expectedResources, "Resources should match!");
			Assert.AreEqual (expectedFileVersion, actualFileVersion, "FileVersion should match!");

			stream.Seek (0, SeekOrigin.Begin);
			var actualChunks = new StringBuilder ();
			var actualDoc = AndroidManifest.Read (stream, (t, c, p) => actualChunks.AppendLine ($"{t}, chunkSize: {c}, position: {p}"));
			Assert.AreEqual (expectedChunks.ToString (), actualChunks.ToString (), "Chunk sizes and ordering should match!");
			actualStrings = actualDoc.Strings;
			actualResources = actualDoc.Resources;
			actualFileVersion = actualDoc.PlatformBuildVersionName;
			ContainsAllStrings (expectedStrings, actualStrings);
			Assert.AreEqual (expectedResources, expectedResources, "Resources should match!");
			Assert.AreEqual (expectedFileVersion, actualFileVersion, "FileVersion should match!");

			Assert.AreEqual (expectedDoc.Document.ToString (), actualDoc.Document.ToString ());

			Assert.AreEqual (binaryManifest.Length, stream.Length, "Stream lengths should match!");
		}
	}
}
