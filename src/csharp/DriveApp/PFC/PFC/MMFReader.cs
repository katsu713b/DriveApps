using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PFC;

public class MMFReader : IDisposable
{
    private MemoryMappedFile _file;
    // Open shared memory
    private MemoryMappedViewAccessor _accessor;

    public MMFReader() { }

    public AdvancedData? GetAdvancedDataOrNull()
    {
        AdvancedData? data = null;
        try
        {
            if (_file == null)
            {
                _file = MemoryMappedFile.OpenExisting("PFC_Latest_AdvancedData");
            }
            if (_file != null && _accessor == null)
            {
                _accessor = _file.CreateViewAccessor();
            }

            var buff = new byte[AdvancedData.DataLength];
            for (int i = 0; i < buff.Length; i++)
            {
                buff[i] = _accessor.ReadByte(i);
            }

            //data = new AdvancedData(buff);
        }
        catch
        {

        }

        return data;
    }

    public void Dispose()
    {
        using (_file)
        using (_accessor){ }
    }
}
