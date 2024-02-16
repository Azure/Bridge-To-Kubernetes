FROM node:lts-alpine@sha256:be8f26834660fd887d22fb2659615f3ef2dd9eaf2a3d544512674eeb7959cafb

WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .

CMD ["npm", "start"]