namespace Cuku.Geo
{
	using Cuku.Geo.Filter;
	using Cuku.ScriptableObject;
	using Sirenix.OdinInspector;

    public class OSMFeatureConfig : SerializedScriptableObject
    {
		[PropertySpace, Title("Data"), FilePath(AbsolutePath = true, RequireExistingPath = true)]
		[InfoBox("City data path in Protocol Buffers format (https://download.geofabrik.de/).")]
		public string CityOSMData;

		[PropertySpace, FolderPath(AbsolutePath = true, RequireExistingPath = true)]
		[InfoBox("Folder path were extracted feature data will be saved.")]
		public string FeaturesDataPath;

		[PropertySpace, InfoBox("Format used to save and parse geo data.")]
		public string GeoFormat;

		[PropertySpace(20), Title("City"), Required, InlineEditor]
		public StringSO CityName;

		/// <summary>
		/// https://franzpc.com/apps/coordinate-converter-utm-to-geographic-latitude-longitude.html
		/// Berlin center point in decimal degrees
		/// Map Datum: WGS 84
		/// Zone: 33
		/// Hemisphere: N
		/// Easting(UTMX): 392000
		/// Northing(UTMY): 5820000
		/// Click: Convert Standard UTM 
		/// Use only 6 decimal points
		/// </summary>
		[PropertySpace, InfoBox("City center coordinates in decimal degrees.")]
		public double[] CenterCoordinates;

		[PropertySpace, InlineEditor, InfoBox("Features to extract.")]
		public FeatureFilter[] Features;
	}
}
