using HttpClientUI.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpClientUI.Models
{
    public class HeaderModel : NotificationObject
    {
        private string m_key;
        public string Key
        {
            get { return m_key; }
            set
            {
                if (value != m_key)
                {
                    m_key = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Key"));
                }
            }
        }

        private string m_value;
        public string Value
        {
            get { return m_value; }
            set
            {
                if (value != m_value)
                {
                    m_value = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Value"));
                }
            }
        }

        public HeaderModel() { }
        public HeaderModel(string k, string v)
        {
            Key = k;
            Value = v;
        }
    }
}
