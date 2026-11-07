--[[--
Pushes words saved by KOReader's Vocabulary Builder to Bookside
(POST /api/words). Only words added since the last push are sent; resending
is safe, since Bookside answers 200 for a word it already has.
]]

local ConfirmBox = require("ui/widget/confirmbox")
local DataStorage = require("datastorage")
local InfoMessage = require("ui/widget/infomessage")
local JSON = require("json")
local LuaSettings = require("luasettings")
local MultiInputDialog = require("ui/widget/multiinputdialog")
local NetworkMgr = require("ui/network/manager")
local SQ3 = require("lua-ljsqlite3/init")
local Trapper = require("ui/trapper")
local UIManager = require("ui/uimanager")
local WidgetContainer = require("ui/widget/container/widgetcontainer")
local http = require("socket.http")
local lfs = require("libs/libkoreader-lfs")
local logger = require("logger")
local ltn12 = require("ltn12")
local socket = require("socket")
local socketutil = require("socketutil")
local _ = require("gettext")
local T = require("ffi/util").template

local VOCAB_DB = DataStorage:getSettingsDir() .. "/vocabulary_builder.sqlite3"

local Bookside = WidgetContainer:extend{
    name = "bookside",
    is_doc_only = false,
}

local function trim(s)
    return s and s:match("^%s*(.-)%s*$")
end

function Bookside:init()
    self.settings = LuaSettings:open(DataStorage:getSettingsDir() .. "/bookside.lua")
    self.ui.menu:registerToMainMenu(self)
end

function Bookside:addToMainMenu(menu_items)
    menu_items.bookside = {
        text = _("Bookside"),
        sorting_hint = "tools",
        sub_item_table = {
            {
                text = _("Push vocabulary"),
                callback = function() self:push() end,
            },
            {
                text = _("Server settings"),
                keep_menu_open = true,
                callback = function() self:editSettings() end,
            },
            {
                text_func = function()
                    local last = self.settings:readSetting("last_sync", 0)
                    return T(_("Last pushed: %1"),
                        last > 0 and os.date("%Y-%m-%d %H:%M", last) or _("never"))
                end,
                keep_menu_open = true,
                callback = function(touchmenu_instance)
                    UIManager:show(ConfirmBox:new{
                        text = _("Send all words again on the next push?"),
                        ok_callback = function()
                            self.settings:saveSetting("last_sync", 0)
                            self.settings:flush()
                            touchmenu_instance:updateItems()
                        end,
                    })
                end,
            },
        },
    }
end

function Bookside:editSettings()
    local dialog
    dialog = MultiInputDialog:new{
        title = _("Bookside server"),
        fields = {
            {
                description = _("Server address"),
                text = self.settings:readSetting("server", ""),
                hint = "https://bookside.example",
            },
            {
                description = _("API token"),
                text = self.settings:readSetting("token", ""),
                text_type = "password",
            },
        },
        buttons = {{
            {
                text = _("Cancel"),
                id = "close",
                callback = function() UIManager:close(dialog) end,
            },
            {
                text = _("Save"),
                callback = function()
                    local fields = dialog:getFields()
                    self.settings:saveSetting("server", (trim(fields[1]):gsub("/+$", "")))
                    self.settings:saveSetting("token", trim(fields[2]))
                    self.settings:flush()
                    UIManager:close(dialog)
                end,
            },
        }},
    }
    UIManager:show(dialog)
    dialog:onShowKeyboard()
end

-- Words added after `since` (unix seconds), oldest first.
local function readWords(since)
    if lfs.attributes(VOCAB_DB, "mode") ~= "file" then
        return {}
    end
    local conn = SQ3.open(VOCAB_DB)
    local res = conn:exec(string.format([[
        SELECT v.word, v.create_time, v.prev_context, v.next_context, t.name
        FROM vocabulary v LEFT JOIN title t ON t.id = v.title_id
        WHERE v.create_time > %d ORDER BY v.create_time;]], since))
    conn:close()

    local words = {}
    for i = 1, res and #res.word or 0 do
        local context = (res.prev_context[i] or "") .. res.word[i] .. (res.next_context[i] or "")
        table.insert(words, {
            term = res.word[i],
            time = tonumber(res.create_time[i]),
            context = trim(context),
            book = res.name[i],
        })
    end
    return words
end

-- Returns the HTTP status and decoded body, or nil and an error message.
local function post(url, token, payload)
    local body = JSON.encode(payload)
    local sink = {}
    -- Bookside looks the definition up before answering: up to ~16 s.
    socketutil:set_timeout(20, 30)
    local code, headers = socket.skip(1, http.request{
        url = url,
        method = "POST",
        headers = {
            ["Authorization"] = "Bearer " .. token,
            ["Content-Type"] = "application/json",
            ["Content-Length"] = tostring(#body),
            ["Accept"] = "application/json",
        },
        source = ltn12.source.string(body),
        sink = ltn12.sink.table(sink),
    })
    socketutil:reset_timeout()
    if headers == nil then
        return nil, tostring(code)
    end
    local ok, decoded = pcall(JSON.decode, table.concat(sink))
    return code, ok and decoded or nil
end

local function isBookError(body)
    return type(body) == "table" and type(body.errors) == "table" and body.errors.book ~= nil
end

function Bookside:push()
    local server = self.settings:readSetting("server", "")
    local token = self.settings:readSetting("token", "")
    if server == "" or token == "" then
        UIManager:show(InfoMessage:new{
            text = _("Set the server address and API token in Bookside → Server settings first."),
        })
        return
    end
    NetworkMgr:runWhenOnline(function()
        Trapper:wrap(function() self:pushWords(server .. "/api/words", token) end)
    end)
end

function Bookside:pushWords(url, token)
    local since = self.settings:readSetting("last_sync", 0)
    local words = readWords(since)
    local cursor = since
    local sent, unlinked, failed, stop = 0, 0, {}, nil

    for i, word in ipairs(words) do
        if not Trapper:info(T(_("Pushing %1 / %2…"), i, #words)) then
            stop = _("Stopped.")
        else
            local payload = { term = word.term, context = word.context, book = word.book }
            local code, body = post(url, token, payload)
            if code == 400 and payload.book and isBookError(body) then
                -- No (single) Bookside book with this title: add the word unlinked.
                payload.book = nil
                code, body = post(url, token, payload)
                if code == 200 or code == 201 then
                    unlinked = unlinked + 1
                end
            end

            if code == 200 or code == 201 then
                sent = sent + 1
            elseif code == 400 then
                logger.warn("Bookside: word rejected", word.term, body)
                table.insert(failed, word.term)
            elseif code == 401 then
                stop = _("Bookside rejected the API token.")
            else
                stop = T(_("Couldn't reach Bookside (%1). Try again later."), tostring(code or body))
            end
        end

        if stop then
            -- Resend this word (and any from the same second) next time.
            cursor = math.max(since, word.time - 1)
            break
        end
        cursor = word.time
    end

    self.settings:saveSetting("last_sync", cursor)
    self.settings:flush()
    Trapper:clear()

    local text = T(_("Sent %1 of %2 new words."), sent, #words)
    if unlinked > 0 then
        text = text .. "\n" .. T(_("%1 had no matching book in Bookside and were added without one."), unlinked)
    end
    if #failed > 0 then
        text = text .. "\n" .. T(_("Rejected: %1"), table.concat(failed, ", "))
    end
    if stop then
        text = text .. "\n" .. stop
    end
    UIManager:show(InfoMessage:new{ text = text })
end

return Bookside
