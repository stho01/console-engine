-- The player: arrow keys move it. The engine draws its Glyph at self.x, self.y.
local Player = {}

local SPEED = 20 -- cells per second
local HELP = "Arrows: move   F5: reload scripts   ESC: quit"

function Player.on_update(self, dt)
    local dx, dy = 0, 0
    if input.held(keys.LEFT) then dx = dx - 1 end
    if input.held(keys.RIGHT) then dx = dx + 1 end
    if input.held(keys.UP) then dy = dy - 1 end
    if input.held(keys.DOWN) then dy = dy + 1 end

    -- Cells are square (12x12 font), so the same speed works on both axes.
    self.x = math.max(0, math.min(console.width - 1, self.x + dx * SPEED * dt))
    self.y = math.max(1, math.min(console.height - 2, self.y + dy * SPEED * dt))
end

-- Extra drawing on top of the entity's Glyph.
function Player.on_render(self)
    console.text(0, 0, HELP, colors.Gray)
end

return Player
