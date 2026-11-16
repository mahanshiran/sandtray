import AppKit
let board=NSPasteboard.general
switch CommandLine.arguments[1] {
case "save":
    let items=(board.pasteboardItems ?? []).map { item -> [String:Data] in
        var values=[String:Data]()
        for type in item.types { if let data=item.data(forType:type) { values[type.rawValue]=data } }
        return values
    }
    let data=try PropertyListSerialization.data(fromPropertyList:items,format:.binary,options:0)
    try data.write(to:URL(fileURLWithPath:CommandLine.arguments[2]))
case "restore":
    let data=try Data(contentsOf:URL(fileURLWithPath:CommandLine.arguments[2]))
    let values=try PropertyListSerialization.propertyList(from:data,options:[],format:nil) as! [[String:Data]]
    board.clearContents()
    board.writeObjects(values.map { value -> NSPasteboardItem in
        let item=NSPasteboardItem()
        for (type,data) in value { item.setData(data,forType:NSPasteboard.PasteboardType(type)) }
        return item
    })
case "text":board.clearContents();board.setString(CommandLine.arguments[2],forType:.string)
case "image":board.clearContents();let data=try Data(contentsOf:URL(fileURLWithPath:CommandLine.arguments[2]));board.setData(data,forType:.png)
case "read":print(board.string(forType:.string) ?? "")
default:break
}
