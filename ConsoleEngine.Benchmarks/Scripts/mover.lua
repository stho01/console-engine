-- A moving game object: arrow keys accelerate it, it bounces off the screen edges. The engine owns x, y, char and
-- color and draws the object, so there is no on_render. Velocity is script state in self.data.
-- GameObjectFrame.MoverBehaviour is the same logic in C#.

local KEY_LEFT, KEY_RIGHT = 0x25, 0x27
local ACCELERATION = 20
local next_id = 0

local Mover = {}

function Mover.on_start(self)
    next_id = next_id + 1
    local id = next_id
    self.data.vx = (id % 5) - 2 + 0.5
    self.data.vy = (id % 3) - 1 + 0.25
end

function Mover.on_update(self, dt)
    local data = self.data
    local vx, vy = data.vx, data.vy
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

    self.x, self.y = x, y
    data.vx, data.vy = vx, vy
end

return Mover
