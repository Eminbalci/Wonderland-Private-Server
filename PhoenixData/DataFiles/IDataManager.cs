using System.Threading.Tasks;

namespace DataFiles;

public interface IDataManager
{
	Task<bool> Load(string file);

	object GetObject(object a);
}
