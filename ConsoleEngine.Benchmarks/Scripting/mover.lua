-- A moving entity: arrow keys accelerate it, it bounces off the screen edges. Position and velocity are the entity's
-- Position and Velocity components; the GlyphRenderSystem draws it. LuaScriptBenchmarks.MoverSystem is the same logic
-- in C#.

local KEY_LEFT, KEY_RIGHT = 0x25, 0x27
local ACCELERATION = 20

local Mover = {}

function Mover.on_update(self, dt)
    local vx, vy = self.vx, self.vy
    if input.held(KEY_LEFT) then vx = vx - ACCELERATION * dt end
    if input.held(KEY_RIGHT) then vx = vx + ACCELERATION * dt end

    local x = self.x + vx * dt
    local y = self.y + vy * dt
    if x < 0 or x >= console.width then
        vx = -vx
        x = self.x
    end
    if y < 0 or y >= console.height then
        vy = -vy
        y = self.y
    end

    self.x, self.y, self.vx, self.vy = x, y, vx, vy
end

return Mover
