   M S B A M L   `   `   `  ����F  BPrototyping, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null)clr-namespace:PrototypingPrototyping  5      *    $Prototyping.Pages.BaseMapDrawingPage   5      R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5      R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      +localclr-namespace:Prototyping     5        N� Title$  Base Map Drawing��5      .��5         Prototyping.BaseLayout     HeaderContent 5	   
   �� 5
       �� Orientation$ 
Horizontal=�6   $��Center=�62   .+�5      �� $��Base Map Drawing��6   $��20(�64   $
��Bold"�6B   5         LeftContent 5   
   �� 5      .+�5      �� $��Mode:��6   $��0,5,0,0q�6)   �� 5      -   5        
 � Tag$ DrawingMode��5   S   .@�5      �� $��Freehand��6#    �� 
IsSelected . 66   �� 5      $��Stamp��6#   �� 5      $��Eraser��6#   �� 5      $��Paint Bucket��6#   5      �� 5      $��Stamp Shape:��6   $��0,10,0,0q�60   �� 5      -   5        $ StampShapeSelector��5   R   .@�5      �� $��Circle��6#    . 64   �� 5      $��Square��6#   �� 5      $��Triangle��6#   �� 5      $��Hexagon��6#   5       �� 5"      $��Brush Size:��6   $��0,10,0,0q�6/   �� 5#      -   5         � Minimum$ 1Q�5#       � Maximum$ 50Q�5$       � Value$ 10Q�5%      $ 	BrushSize��5&      5'      �� 5)      $��Layer:��6   $��0,10,0,0q�6*   �� 5*      -   5        $ LayerSelector��5*   M   .@�5+      �� $��
Base (Red)��6#    . 68   �� 5,      $��Middle (Green)��6#   �� 5-      $��
Top (Blue)��6#   5.      �� 50      -   5        $��Restrict to Base Layer��50      $! RestrictToBaseLayerCheckbox��6=   	 d� 	IsChecked$	 FalseY�51      6[   �� 53      -   5        $
��Undo��53      $��0,10,0,0q�6(   $ 
UndoButton��6S   �� 54      -   5        $
��Redo��54      $��0,5,0,0q�6(   $ 
RedoButton��6R   �� 55      -   5        $��Clear��55      $��0,5,0,0q�6)   6:   �� 57      -	   5        $ 
SaveButton��57      $��Save Canvas��6*   $��15q�6Y   58      
   FooterContent
 5<   
   ]� 5=      $��Stretch=�6   .+�5>      � $��Center=�6   $	��250��64    � ColumnDefinitions 5?      �� 5@      $
��.25* �6+   �� 5A      $
��.75* �6+   �� 5B      $
��.75* �6+   �� 5C      $
��.25* �6+   .+�5F      �� -
   5        $
��Back��5F      $��5q�6,   $��1��67   6G   �� 5H      -   5        $
��Next��5H      $��5q�6,   $��2��67   6G   5I      5J         RightContent 5N   
   � 5O      .+�5P      �� -   5        $ 
DrawCanvas��5P      
�������5T      �� 5U      $ PreviewCanvas��6   5V      5X      5Y      