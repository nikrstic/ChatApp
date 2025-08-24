using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatShared.Models
{
    public class ActiveClients
    {
        private static ObservableCollection<string> _korisnici = new ObservableCollection<string>();

        // elegantni getter

        public static ObservableCollection<string> Korisnici => _korisnici;

        public static void Add(string nickname)
        {
            if(!_korisnici.Contains(nickname)) 
                _korisnici.Add(nickname);

        }
        public static void Remove(string nickname)
        {
            if (_korisnici.Contains(nickname))
                _korisnici.Remove(nickname);
        }
        
    }
}
