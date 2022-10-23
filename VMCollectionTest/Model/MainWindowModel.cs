using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMCollectionTest.Model
{
    public class MainWindowModel : Livet.NotificationObject
    {
        public ObservableCollection<ItemModel> Items { get; set; }

        public MainWindowModel()
        {
            Items = new ObservableCollection<ItemModel>();
            Items.Add(new ItemModel());
            Items.Add(new ItemModel());
            Items.Add(new ItemModel());
            Items.Add(new ItemModel());
        }
    }
}
