// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2014 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}
namespace WebSharper.Html.Server

open System
open System.Collections.Generic
open System.IO
open System.Web.UI
open System.Xml

/// Provides types for expressing HTML values.
[<AutoOpen>]
module Html =

    type IControl = WebSharper.Html.Client.IControl

    /// Represents HTML tags.
    type TagContent =
        {
            Name : string
            Attributes : list<Attribute>
            Contents : list<Element>
            Annotation : option<IControl>
        }

    /// Represents HTML attributes.
    and Attribute =
        {
            Name : string
            Value : string
        }

        interface INode with
            member this.Node = AttributeNode this

    /// Represents nodes as an interface, for convenience.
    and INode =
        abstract member Node : Node

    /// Represents elements as an interface, for convenience.
    and IElement =
        abstract member Element : Element

    /// Represents nodes.
    and Node =
        | AttributeNode of Attribute
        | ContentNode of Element

    /// Represents HTML/XML contents.
    and  Element =
        | TagContent of TagContent
        | TextContent of string
        | VerbatimContent of string
        | CommentContent of string

        interface IElement with
            member this.Element = this

        interface INode with
            member this.Node = ContentNode this

        /// Collects all annotations from contained elements.
        member this.CollectAnnotations () : list<IControl> =
            match this with
            | TagContent content ->
                let nested =
                    content.Contents
                    |> List.map (fun content -> content.CollectAnnotations())
                    |> List.concat
                match content.Annotation with
                | Some a -> [a]
                | None -> []
                |> List.append nested
            | TextContent _
            | VerbatimContent _
            | CommentContent _ -> []

    /// Constructs a new HTML element.
    let NewTag (name: string) (elements: seq<#INode>) =
        let attrs =
            elements
            |> Seq.choose (fun x ->
                match x.Node with
                | AttributeNode x -> Some x
                | _ -> None)
            |> Seq.toList

        let selCnt (x: INode) =
            match x.Node with
            | ContentNode x -> Some x
            | _ -> None

        let contents =
            elements
            |> Seq.choose selCnt
            |> Seq.toList
        {
            Name = name
            Attributes = attrs
            Contents = contents
            Annotation = None
        }
        |> TagContent

    let (-<) (el: Element) (elements: seq<#INode>) : Element =
        match el with
        | TagContent el ->
            let newAttrs =
                elements
                |> Seq.choose (fun x ->
                    match x.Node with
                    | AttributeNode x -> Some x
                    | _ -> None)
                |> Seq.toList
            let newContent =
                elements
                |> Seq.choose (fun x ->
                    match x.Node with
                    | ContentNode x -> Some x
                    | _ -> None)
                |> Seq.toList
            {
                Name = el.Name
                Attributes = el.Attributes @ newAttrs
                Contents = el.Contents @ newContent
                Annotation = el.Annotation
            }
            |> TagContent
        | VerbatimContent _
        | TextContent _
        | CommentContent _ ->
            failwith "Invalid HTML operation - can't append to a non-tag node"

    /// Constructs a new attribute value.
    let NewAttr name value =
        {
            Name = name
            Value = value
        }

    /// Sets an annotation on the element.
    /// If the element is a non-tag element, this operation is a no-op.
    let Annotate (x: IControl) (e: Element) =
        match e with
        | TagContent e ->
            { e with Annotation = Some x }
            |> TagContent
        | VerbatimContent _
        | TextContent _
        | CommentContent _ -> e

    /// Represents HTML element identifiers.
    type Id = string

    /// Represents documents with annotations mapped by HTML identifiers.
    type Document =
        {
            Annotations : Map<Id, IControl>
            Body : Element
        }

    /// Resolves the annotations by giving them HTML identifiers. The function
    /// `isUsed` tests if a given identifier should not be used by the document.
    let Resolve (isUsed: Id -> bool) (document: TagContent) =
        let ids = Dictionary<Id,unit>()
        let annotations = Dictionary<Id,IControl>()
        let showId (k: int) = String.Format("id{0:x}", k)
        let newId () =
            let mutable k  = ids.Count
            let mutable id = showId k
            while ids.ContainsKey id || isUsed id do
                k <- k + 1
                id <- showId k
            ids.[id] <- ()
            id
        let rec transform (element: TagContent) =
            let mutable attributes = []
            let mutable id = None
            for x in element.Attributes do
                match x.Name.ToLower() with
                | "id" -> ids.[x.Value] <- (); id <- Some x.Value
                | _ -> ()
                let attribute = { Name = x.Name; Value = x.Value }
                attributes <- attribute :: attributes
            match element.Annotation with
            | None -> ()
            | Some a ->
                let id =
                    match id with
                    | Some id   -> id
                    | None      ->
                        let id = newId ()
                        attributes <- { Name = "id"; Value = id } :: attributes
                        id
                annotations.[id] <- a
            let mutable contents = []
            for x in element.Contents do
                match x with
                | TagContent e ->
                    contents <- transform e :: contents
                | CommentContent e ->
                    contents <- CommentContent e :: contents
                | VerbatimContent e ->
                    contents <- VerbatimContent e :: contents
                | TextContent e ->
                    contents <- TextContent e :: contents
            {
                Name = element.Name // was: document.Name??
                Attributes = List.rev attributes
                Contents = List.rev contents
                Annotation = None
            }
            |> TagContent
        {
            Body = transform document
            Annotations =
                seq { for kv in annotations -> (kv.Key, kv.Value) }
                |> Map.ofSeq
        }

    let private IsSelfClosingTag (name: string) =
        [
            "AREA"
            "BASE"
            "BASEFONT"
            "BR"
            "COL"
            "FRAME"
            "HR"
            "IMG"
            "INPUT"
            "ISINDEX"
            "LINK"
            "META"
            "PARAM"
        ]
        |> List.exists (fun x -> x.ToLower() = name.ToLower())


    /// Writes HTML nodes to an XmlWriter.
    [<Sealed>]
    type Writer (writer: HtmlTextWriter) =

        new (writer: TextWriter) =
            new Writer (new HtmlTextWriter(writer))

        // Renders an element without content and with self
        // closing tagE.g. <img class="c" />
        member private this.RenderElementWithSelfClosingTag name (attrs: list<Attribute>) =
            writer.WriteBeginTag(name)
            for a in attrs do
                writer.WriteAttribute(a.Name, a.Value)
            writer.Write(HtmlTextWriter.SelfClosingTagEnd)

        // Renders tag with content.
        // E.g. <div class="c">...</div>
        member private this.RenderElementWithContent name
                (attrs: list<Attribute>)
                (contents : list<Element>) =

            writer.WriteBeginTag(name)
            for a in attrs do
                writer.WriteAttribute(a.Name, a.Value)
            writer.Write(HtmlTextWriter.TagRightChar)

            // Render contents
            for content in contents do
                this.Write content
            writer.WriteEndTag(name)

        /// Writes a content node.
        member this.Write(x: Element) =
            match x with
            | TagContent element ->
                match element.Contents with
                | [] when IsSelfClosingTag element.Name ->
                    this.RenderElementWithSelfClosingTag element.Name element.Attributes
                | contents ->
                    this.RenderElementWithContent element.Name element.Attributes contents
            | TextContent text ->
                writer.WriteEncodedText text
            | VerbatimContent text ->
                writer.Write text
            | CommentContent comment ->
                writer.Write "<!--"
                comment.Replace("--", " - - ")
                |> writer.Write
                writer.WriteLine "-->"

        /// Writes a text node.
        member this.Write(text: string) =
            writer.WriteEncodedText text

        /// Returns the inner XmlWriter.
        member this.Writer = writer

        interface IDisposable with
            member this.Dispose() =
                (writer :> IDisposable).Dispose()
