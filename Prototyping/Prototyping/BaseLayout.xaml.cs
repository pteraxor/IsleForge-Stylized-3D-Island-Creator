using System.Windows.Controls;

namespace Prototyping
{
    public partial class BaseLayout : UserControl
    {
        public BaseLayout()
        {
            InitializeComponent();
        }

        public object HeaderContent
        {
            get => Header.Content;
            set => Header.Content = value;
        }

        public object LeftContent
        {
            get => Left.Content;
            set => Left.Content = value;
        }

        public object FooterContent
        {
            get => Footer.Content;
            set => Footer.Content = value;
        }

        public object RightContent
        {
            get => Right.Content;
            set => Right.Content = value;
        }
    }
}
