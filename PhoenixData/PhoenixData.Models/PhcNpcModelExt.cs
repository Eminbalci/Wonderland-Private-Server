using System.ComponentModel;
using DataFiles;

namespace PhoenixData.Models;

public class PhcNpcModelExt
{
	protected BindingList<PhxItemInfo> items;

	public BindingList<PhxItemInfo> ItemsDropped => items;
}
