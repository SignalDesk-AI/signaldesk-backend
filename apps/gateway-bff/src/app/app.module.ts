import { Module } from '@nestjs/common';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { ConfigModule } from './config/config.module';
import { HealthModule } from './health/health.module';
import { LoggingModule } from './logging/logging.module';
import { AuthModule } from './auth/auth.module';
import { ProxyModule } from './proxy/proxy.module';
import { RealtimeModule } from './realtime/realtime.module';
import { RequestContextModule } from './request-context/request-context.module';

@Module({
  imports: [
    ConfigModule,
    HealthModule,
    LoggingModule,
    AuthModule,
    RequestContextModule,
    ProxyModule,
    RealtimeModule,
  ],
  controllers: [AppController],
  providers: [AppService],
})
export class AppModule {}
