using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMCollectionTest.Model
{
    public class ItemModel : Livet.NotificationObject
    {
        public string Name { get; set; } = "InitialName";

        public ItemModel()
        { }
    }
}
