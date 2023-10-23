FROM --platform=$BUILDPLATFORM mcr.microsoft.com/cbl-mariner/base/nodejs:18.17.1-2-cm2.0.20231004
ARG TARGETARCH
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .

CMD ["npm", "start"]