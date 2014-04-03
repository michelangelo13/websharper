﻿// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2013 IntelliFactory
//
// GNU Affero General Public License Usage
// WebSharper is free software: you can redistribute it and/or modify it under
// the terms of the GNU Affero General Public License, version 3, as published
// by the Free Software Foundation.
//
// WebSharper is distributed in the hope that it will be useful, but WITHOUT
// ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
// FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License
// for more details at <http://www.gnu.org/licenses/>.
//
// If you are unsure which license is appropriate for your use, please contact
// IntelliFactory at http://intellifactory.com/contact.
//
// $end{copyright}

[<IntelliFactory.WebSharper.Core.Attributes.Name "Arrays2D">]
[<IntelliFactory.WebSharper.Core.Attributes.Proxy
    "Microsoft.FSharp.Collections.Array2DModule, \
     FSharp.Core, Culture=neutral, \
     PublicKeyToken=b03f5f7f11d50a3a">]
module private IntelliFactory.WebSharper.Array2DModuleProxy

module F = IntelliFactory.WebSharper.IntrinsicFunctionProxy

[<JavaScript>]
[<Inline>]
[<Name "length1">]
let Length1 (arr: 'T[,]) = F.GetArray2DLength1 arr

[<Inline>]
[<JavaScript>]
[<Name "length2">]
let Length2 (arr: 'T[,]) = F.GetArray2DLength2 arr

[<Inline>]
[<JavaScript>]
let Get (array: 'T[,]) (n:int) (m:int) = F.GetArray2D array n m

[<Inline>]
[<JavaScript>]
let Set (array: 'T[,]) (n:int) (m:int) (x:'T) = F.SetArray2D array n m x

[<JavaScript>]
[<Name "zeroCreate">]
let ZeroCreate (n:int) (m:int) = F.Array2DZeroCreate n m
    
[<Inline>]
[<JavaScript>]
[<Name "create">]
let Create n m (x:'T) =
    let arr = As<'T[,]>(Array.init n (fun _ -> Array.create m x))
    arr?dims <- 2
    arr
     
[<JavaScript>]
[<Name "init">]
let Initialize n m f = 
    let array = ZeroCreate n m : 'T[,]  
    for i = 0 to n - 1 do 
        for j = 0 to m - 1 do 
            array.[i, j] <- f i j
    array

[<JavaScript>]
[<Name "iter">]
let Iterate f array = 
    let count1 = F.GetArray2DLength1 array 
    let count2 = F.GetArray2DLength2 array 
    for i = 0 to count1 - 1 do 
      for j = 0 to count2 - 1 do 
        f array.[i,j]

[<JavaScript>]
[<Name "iteri">]
let IterateIndexed (f : int -> int -> 'T -> unit) (array:'T[,]) =
    let count1 = F.GetArray2DLength1 array 
    let count2 = F.GetArray2DLength2 array 
    for i = 0 to count1 - 1 do 
      for j = 0 to count2 - 1 do 
        f i j array.[i,j]

[<JavaScript>]
[<Name "map">]
let Map f array = 
    Initialize (F.GetArray2DLength1 array) (F.GetArray2DLength2 array) (fun i j -> f array.[i,j])

[<JavaScript>]
[<Name "mapi">]
let MapIndexed f array = 
    Initialize (F.GetArray2DLength1 array) (F.GetArray2DLength2 array) (fun i j -> f i j array.[i,j])

[<JavaScript>]
[<Name "copy">]
let Copy array = 
    Initialize (F.GetArray2DLength1 array) (F.GetArray2DLength2 array) (fun i j -> array.[i,j])
