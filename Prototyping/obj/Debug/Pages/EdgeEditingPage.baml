   M S B A M L   `   `   `  ����F  BPrototyping, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null)clr-namespace:PrototypingPrototyping  5      '    !Prototyping.Pages.EdgeEditingPage   5      \ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5      R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      +localclr-namespace:Prototyping     5        N� Title$  EdgeEditingPage��5	      .��5         Prototyping.BaseLayout     HeaderContent 5   
   �� 5       �� Orientation$ 
Horizontal=�6   $��Center=�62   .+�5      �� $��Edge Editing��6   $��20(�60   $
��Bold"�6>   5         LeftContent 5   
   �� 5      .+�5      �� $��Brush Size:��6   $��0,10,0,0q�6/   �� 5      -   5         � Minimum$ 1Q�5       � Maximum$ 50Q�5       � Value$ 10Q�5      
 � Tag$ 	BrushSize��5      5      �� 5      $��Edge Style:��6   $��0,10,0,0q�6/   �� 5      -   5        $ EdgeSelector��5   Q   .@�5       �� $��Sheer (Black)��6#    �� 
IsSelected . 6;   �� 5!      $��	Smooth ()��6#   �� 5"      $��Slope ()��6#   5#      �� 5%      -   5        $ 
SaveButton��5%      $��Save Canvas��6*   $��15q�6Y   5&      	   FooterContent	 5*   
   ]� 5+      $��Stretch=�6   .+�5,      � $��Center=�6   $	��250��64   
 � ColumnDefinitions
 5-      �� 5.      $
��.25* �6+   �� 5/      $
��.75* �6+   �� 50      $
��.75* �6+   �� 51      $
��.25* �6+   .+�54      �� -   5        $
��Back��54      $��5q�6,   $��1��67   6G   �� 56      -   5        $
��Next��56      $��5q�6,   $��2��67   6G   57      58         RightContent 5<   
   � 5=      .+�5>      �� $ BasemapCanvas��6   
�������6-   �� 5?      -   5        $ 
DrawCanvas��5?      5B      5D      5F      5G      