using Newtonsoft.Json.Linq;

public interface ISave
{
	void ToData(JObject jObject);

	void FromData(JObject jObject);
}
